using UnityEngine;
using System;

/// <summary>
/// 敌人 AI 大脑（轻量状态机，单文件四状态）。
///
/// 状态流转：
///   Idle  ——目标进入 chaseRange——▶  Chase
///   Chase ——目标进入 attackRange——▶ Attack（进入时立即出手一次）
///   Chase ——目标超出 loseTargetRange——▶ Idle（脱战）
///   Attack——目标超出 attackRange×1.2——▶ Chase（滞回区间防止边界抖动）
///   任意  ——EnemyHealth.OnDied——▶   Dead（停止一切行为）
///
/// 设计取舍（面试讲解点）：
///   1. 为何不用"状态类"分层？当前只有 4 个状态、状态逻辑均在 10 行以内，
///      enum + switch 单文件最直观；状态数膨胀或状态需要携带独立数据时，
///      再按"一状态一类 + 状态基类"重构（开闭原则，届时本类只保留切换表）。
///   2. 目标定位：用 OverlapSphere + LayerMask 索敌，不硬编码 Tag/单例，
///      以后换成 ITargetProvider 注入即可支持多目标/仇恨系统。
///   3. 攻击协议与玩家完全一致：只调 IDamageable.TakeDamage(DamageInfo)，
///      不感知 PlayerHealth 具体类（依赖倒置，与 AttackHitDetection 同一约定）。
///   4. 距离全部用平方距离比较，避免每帧开方。
///
/// 池化接入：实现 IPoolable，OnSpawned 复位状态机与计时器（由 PoolManager 取出对象时调用）。
///
/// 预留扩展（当前不做）：
///   - PatrolState：需要巡逻点数据（Transform[] 或 ScriptableObject 路径资产）
///   - HurtState：订阅 EnemyHealth.OnDamaged 切入，配合受击动画/硬直计时
///   - 攻击前摇：Attack 出手时机改由 Animation Event 驱动
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[AddComponentMenu("RPG/Enemy/Enemy Brain")]
public class EnemyBrain : MonoBehaviour, IPoolable
{
    private enum State { Idle, Chase, Attack, Dead }

    [Header("配置")]
    [Tooltip("敌人配置（索敌/攻击/移动参数来源）")]
    [SerializeField] private EnemyProfile profile;

    [Header("目标")]
    [Tooltip("追击目标。不填则运行时按 targetMask 自动索敌")]
    [SerializeField] private Transform target;

    [Tooltip("目标所在层（只勾 Player）")]
    [SerializeField] private LayerMask targetMask = 1 << 6;

    [Tooltip("自动索敌的最大搜索半径")]
    [SerializeField] private float findTargetRadius = 100f;

    [Header("移动")]
    [Tooltip("转向速度（度/秒），避免瞬间掉头")]
    [SerializeField] private float turnSpeed = 540f;

    [Header("重力/地面")]
    [Tooltip("重力加速度（负数向下）")]
    [SerializeField] private float gravity = -20f;

    [Tooltip("地面检测起点：从脚下往上偏移多少米开始向下射，避免起点埋进地面")]
    [SerializeField] private float groundCheckStart = 0.1f;

    [Tooltip("地面检测距离")]
    [SerializeField] private float groundCheckDistance = 5f;

    [Tooltip("可作为地面的层（勾 Ground 和 Default，别勾 Enemy/Player）")]
    [SerializeField] private LayerMask groundMask = (1 << 3);

    [Header("调试")]
    [Tooltip("开发期打印状态切换/索敌/攻击日志，打包前关闭")]
    [SerializeField] private bool debugLog = true;

    private EnemyHealth _health;
    private State _state = State.Idle;
    private float _attackTimer;
    private float _distLogTimer;
    private float _verticalVelocity; // 重力累计的垂直速度
    private bool _isGrounded;        // 本帧是否接地

    // ==================== 对外事件（给 EnemyAnimator / 掉落 / 其他扩展） ====================

    /// <summary>状态切换时触发（供 EnemyAnimator 在回调里读 IsAttacking/IsDead 等只读属性）</summary>
    public event Action StateChanged;

    /// <summary>真正出手那一瞬间触发（供 EnemyAnimator Trigger 攻击动画）</summary>
    public event Action AttackExecuted;

    // ==================== 状态只读暴露（动画、调试外部可读） ====================

    public bool IsAttacking => _state == State.Attack;
    public bool IsDead => _state == State.Dead;
    public bool IsChasing => _state == State.Chase;
    public bool IsIdle => _state == State.Idle;

    /// <summary>配置资产（攻击组件读取伤害/射程用）</summary>
    public EnemyProfile Profile => profile;

    /// <summary>当前追击/攻击目标（攻击组件读取，可能为 null）</summary>
    public Transform Target => target;

    /// <summary>索敌缓冲区（NonAlloc，复用避免 GC）</summary>
    private static readonly Collider[] _targetBuffer = new Collider[4];

    void Awake()
    {
        _health = GetComponent<EnemyHealth>();
    }

    void OnEnable()
    {
        if (_health != null) _health.OnDied += HandleDied;
        _verticalVelocity = 0f;
    }

    void OnDisable()
    {
        if (_health != null) _health.OnDied -= HandleDied;
    }

    void Start()
    {
        if (target == null) TryFindTarget();
    }

    void Update()
    {
        if (_state == State.Dead || profile == null) return;

        // 重力在所有存活状态下生效（Idle/Chase/Attack 都要落地）
        ApplyGravity();

        // 目标未指定且自动索敌失败 → 待机并持续重试（玩家可能尚未生成）
        if (target == null)
        {
            TryFindTarget();
            if (target == null) return;
        }

        // 水平距离判定：剔除 Y 轴。
        // 敌人可能半埋地下做视觉对齐（y 偏移），玩家在地面上，
        // 若用三维距离，Y 差会顶掉整个 attackRange，导致永远进不了攻击态。
        // AI 的移动/转向本来也只在水平面进行，距离口径必须一致。
        // （以后若做飞行敌人，再引入单独的高度判定参数。）
        Vector3 diff = target.position - transform.position;
        diff.y = 0f;
        float sqrDist = diff.sqrMagnitude;

        switch (_state)
        {
            case State.Idle: TickIdle(sqrDist); break;
            case State.Chase: TickChase(sqrDist); break;
            case State.Attack: TickAttack(sqrDist); break;
        }
    }

    // ==================== 状态行为 ====================

    /// <summary>统一的状态切换入口：集中打日志，排查"AI 没反应"类问题</summary>
    private void SetState(State next)
    {
        if (_state == next) return;
        if (debugLog)
            Debug.Log($"[EnemyBrain] {name}: {_state} → {next}（距目标 {(target != null ? Vector3.Distance(transform.position, target.position).ToString("F1") : "无目标")}m）", this);
        _state = next;
        StateChanged?.Invoke();
    }

    private void TickIdle(float sqrDist)
    {
        if (sqrDist <= profile.chaseRange * profile.chaseRange)
            SetState(State.Chase);
    }

    private void TickChase(float sqrDist)
    {
        if (sqrDist > profile.loseTargetRange * profile.loseTargetRange)
        {
            SetState(State.Idle); // 脱战
            return;
        }

        if (sqrDist <= profile.attackRange * profile.attackRange)
        {
            // 进入攻击态时计时器直接置满：贴脸立即出手一次，符合直觉
            SetState(State.Attack);
            _attackTimer = profile.attackInterval;
            return;
        }

        // 节流距离日志：直观看到追击过程中距离卡在哪（排查"贴身但进不了攻击态"）
        if (debugLog)
        {
            _distLogTimer -= Time.deltaTime;
            if (_distLogTimer <= 0f)
            {
                _distLogTimer = 0.5f;
                Debug.Log($"[EnemyBrain] {name} 追击中，距目标 {Mathf.Sqrt(sqrDist):F2}m（进入攻击需 ≤ {profile.attackRange}m）", this);
            }
        }

        MoveTowardsTarget();
    }

    private void TickAttack(float sqrDist)
    {
        FaceTarget();

        // 滞回（1.2x）：目标在攻击距离边缘小幅移动时不在 Chase/Attack 间高频抖动
        float exitRange = profile.attackRange * 1.2f;
        if (sqrDist > exitRange * exitRange)
        {
            SetState(State.Chase);
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= profile.attackInterval)
        {
            _attackTimer = 0f;
            AttackOnce();
        }
    }

    // ==================== 行为原语 ====================

    /// <summary>
    /// 单次出手：只抛事件，不结算伤害。
    /// 伤害方式由身上挂的攻击组件决定——EnemyMeleeAttack 直接扣血 / EnemyRangedAttack 吐炸弹，
    /// Brain 因此可被近战/远程怪原样复用。
    /// </summary>
    private void AttackOnce()
    {
        AttackExecuted?.Invoke();
    }

    private void MoveTowardsTarget()
    {
        FaceTarget();

        Vector3 dir = target.position - transform.position;
        dir.y = 0f; // 只在水平面追击，不平地地形由以后的地面吸附/导航处理
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.position += dir.normalized * (profile.moveSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 手动重力：不引入 Rigidbody，避免和 transform.position 移动、后续路径算法冲突。
    /// 接地时直接把 Y 贴到命中点，仅靠钳速度会慢慢穿过地面 Collider（无 Rigidbody 时物理不阻挡）。
    /// </summary>
    private void ApplyGravity()
    {
        // 地面检测：从脚下略上方往下射，避免起点埋进地面导致误判
        Vector3 checkOrigin = transform.position + Vector3.up * groundCheckStart;
        bool hitGround = Physics.Raycast(checkOrigin, Vector3.down, out RaycastHit hit,
                                      groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);

        _isGrounded = hitGround;

        if (_isGrounded && _verticalVelocity <= 0f)
        {
            // 落地：直接贴到命中点 Y。无 Rigidbody 时物理不会阻挡，只钳速度会慢慢穿过地面
            Vector3 pos = transform.position;
            pos.y = hit.point.y;
            transform.position = pos;
            _verticalVelocity = 0f;
        }
        else
        {
            _verticalVelocity += gravity * Time.deltaTime;
            transform.position += Vector3.up * _verticalVelocity * Time.deltaTime;
        }
    }

    private void FaceTarget()
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void TryFindTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, findTargetRadius, _targetBuffer, targetMask);
        if (count > 0)
        {
            target = _targetBuffer[0].transform;
            if (debugLog)
                Debug.Log($"[EnemyBrain] {name} 已锁定目标：{target.name}", this);
        }
    }

    private void HandleDied()
    {
        SetState(State.Dead);
    }

    // ==================== IPoolable（由 PoolManager 调用） ====================

    /// <summary>池取出时调用：回到 Idle、清攻击计时。血量重置由 EnemyHealth.OnSpawned 负责</summary>
    public void OnSpawned()
    {
        _state = State.Idle;
        _attackTimer = 0f;
        _verticalVelocity = 0f;
    }

    /// <summary>回收进池前调用：当前无运行期外部引用需要清理</summary>
    public void OnDespawned() { }

    // ==================== 编辑器可视化 ====================

    void OnDrawGizmosSelected()
    {
        if (profile == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, profile.chaseRange);   // 追击触发范围

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, profile.attackRange);  // 攻击范围
    }
}
