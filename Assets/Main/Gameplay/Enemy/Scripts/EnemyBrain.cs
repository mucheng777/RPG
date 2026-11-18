using UnityEngine;
using UnityEngine.AI;
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
///   5. 移动交给 NavMeshAgent：Brain 只决定"何时走、去哪、何时停"，
///      路径计算、贴地、爬坡、怪与怪之间的避障全部由 Agent 负责。
///      Brain 与 Agent 之间只有三个接触点：SetDestination（告诉它去哪）、
///      isStopped（走/停开关）、Warp（池复用时同步位置）。
///      不再手写 transform.position 位移和重力——两套位移并存会互相拉扯导致抖动。
///
/// 池化接入：实现 IPoolable，OnSpawned 复位状态机与计时器（由 PoolManager 取出对象时调用）。
///
/// 预留扩展（当前不做）：
///   - PatrolState：需要巡逻点数据（Transform[] 或 ScriptableObject 路径资产）
///   - HurtState：订阅 EnemyHealth.OnDamaged 切入，配合受击动画/硬直计时
///   - 攻击前摇：Attack 出手时机改由 Animation Event 驱动
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(NavMeshAgent))]
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
    [Tooltip("转向速度（度/秒），同步给 Agent；攻击态原地转身也用它")]
    [SerializeField] private float turnSpeed = 540f;

    [Tooltip("寻路目的地刷新间隔（秒）。玩家每帧都在动，但每帧 SetDestination 浪费，定期追一次就够")]
    [SerializeField] private float repathInterval = 0.2f;

    [Header("调试")]
    [Tooltip("开发期打印状态切换/索敌/攻击日志，打包前关闭")]
    [SerializeField] private bool debugLog = true;

    private EnemyHealth _health;
    private NavMeshAgent _agent;
    private State _state = State.Idle;
    private float _attackTimer;
    private float _distLogTimer;
    private float _repathTimer;

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
        _agent = GetComponent<NavMeshAgent>();

        // 移动参数统一从 Profile 喂给 Agent，Inspector 里 Agent 的 Speed 只是占位值
        if (profile != null) _agent.speed = profile.moveSpeed;
        _agent.angularSpeed = turnSpeed;

        // 停在哪里由状态机的 attackRange 决定，不让 Agent 自己提前减速刹停
        _agent.stoppingDistance = 0f;
        _agent.isStopped = true; // 出生默认 Idle，不寻路
    }

    void OnEnable()
    {
        if (_health != null) _health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        if (_health != null) _health.OnDied -= HandleDied;
    }

    void Start()
    {
        if (target == null) TryFindTarget();
        WarnIfNotOnNavMesh();
    }

    void Update()
    {
        if (_state == State.Dead || profile == null) return;

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

    /// <summary>统一的状态切换入口：集中打日志 + 集中管 Agent 的走/停</summary>
    private void SetState(State next)
    {
        if (_state == next) return;
        if (debugLog)
            Debug.Log($"[EnemyBrain] {name}: {_state} → {next}（距目标 {(target != null ? Vector3.Distance(transform.position, target.position).ToString("F1") : "无目标")}m）", this);
        _state = next;

        // 移动开关集中在状态切换处：只有 Chase 让 Agent 跑，其余状态全部站定。
        // 好处是"移动权限"只有一个真相源，不会出现某个状态忘了停。
        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = next != State.Chase;
            // Chase 时让 Agent 沿路径自动转向；其余状态把旋转权交回 FaceTarget（攻击态要原地脸朝玩家）
            _agent.updateRotation = next == State.Chase;

            if (next == State.Chase)
                _repathTimer = 0f;  // 进追击立刻刷一次目的地，不等下一个刷新周期
            else
                _agent.ResetPath(); // 清掉残路径，防止下次切回 Chase 时先走一截旧路
        }

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

        // 路径怎么绕、怎么贴地、怎么躲开其他怪，都是 Agent 的事；
        // Brain 只定期告诉它"玩家现在在哪"。
        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f)
        {
            _repathTimer = repathInterval;
            if (_agent.isOnNavMesh)
                _agent.SetDestination(target.position);
        }
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

    /// <summary>不在 NavMesh 上时 Agent 会拒绝寻路，提前打日志省得对着"怪不动"发呆</summary>
    private void WarnIfNotOnNavMesh()
    {
        if (!_agent.isOnNavMesh)
            Debug.LogWarning($"[EnemyBrain] {name} 不在 NavMesh 上，无法追击。检查：1) 场景是否烘焙（NavMeshSurface → Bake）2) 出生点是否落在蓝色可行走区域内", this);
    }

    // ==================== IPoolable（由 PoolManager 调用） ====================

    /// <summary>池取出时调用：回到 Idle、清攻击计时。血量重置由 EnemyHealth.OnSpawned 负责</summary>
    public void OnSpawned()
    {
        _state = State.Idle;
        _attackTimer = 0f;

        // 池复用关键坑：池只 SetPositionAndRotation 改了 Transform，
        // Agent 内部还记着上一世的位置，下一帧会把怪拉回原地（或路径瞬间乱掉）。
        // Warp = 把 Agent 内部位置同步到当前 Transform，并吸附到最近的 NavMesh 点。
        // 注意顺序：池是先改 Transform 再调 OnSpawned，所以这里读到的就是新出生点。
        _agent.Warp(transform.position);
        _agent.ResetPath();
        _agent.isStopped = true;
        WarnIfNotOnNavMesh();
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
