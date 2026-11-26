using UnityEngine;

/// <summary>
/// 四段连击战斗逻辑（极简版）。
///
/// Animator Controller 约定：
///   Attack1 ──ExitTime=1──► Attack2 或 Empty
///   Attack2 ──ExitTime=1──► Attack3 或 Empty
///   Attack3 ──ExitTime=1──► Attack4 或 Empty
///   Attack4 ──ExitTime=1──► Empty
///
/// Phase 维护方式
///   UpdateAttack 每帧读 Animator 当前状态——只要不在 Attack1~4，
///   就认为攻击结束 → 有缓冲输入则起手，否则回 Idle。
///   不再依赖 SMB + 协程链路，避免 Animator 过渡配置有一点问题就卡死。
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    private enum CombatPhase { Idle, Attacking }

    [Header("连击窗口")]
    [Tooltip("当前段 Attack 动画播到此进度后才允许推进下一段（normalizedTime）")]
    [SerializeField] private float comboWindowStart = 0.75f;

    [Header("输入缓冲")]
    [Tooltip("缓冲输入有效时长（秒），超时丢弃")]
    [SerializeField] private float inputBufferTime = 0.15f;

    [Tooltip("Animator 攻击状态所在的 Layer 索引（和 AttackHitDetection 保持一致）")]
    [SerializeField] private int attackLayerIndex = 1;

    [Header("软锁定（出手自动转向最近敌人）")]
    [Tooltip("敌人所在层（只勾 Enemy）")]
    [SerializeField] private LayerMask enemyMask = 1 << 7;

    [Tooltip("锁定搜索半径")]
    [SerializeField] private float lockRadius = 4f;

    [Tooltip("只在角色前方这个角度内锁定（度，360=全方向）")]
    [SerializeField] private float lockAngle = 180f;

    private static readonly int AttackTriggerHash = Animator.StringToHash("AttackTrigger");

    private Animator _animator;
    private readonly Collider[] _enemyBuffer = new Collider[8];

    private CombatPhase _phase = CombatPhase.Idle;
    private int _comboIndex;   // 当前段 1~4，0=未开始
    private bool _comboBuffered;
    private float _bufferTimer;
    private float _attackStartTime;   // 本次攻击的起手时间

    public bool IsAttacking => _phase != CombatPhase.Idle;

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
    }

    // ==================== 对外入口 ====================

    public void TryAttack()
    {
        if (_animator == null) return;

        // 软锁定：出手瞬间自动转向最近的敌人，不用自己对准
        FaceNearestEnemy();

        switch (_phase)
        {
            case CombatPhase.Idle:
                StartAttack(1);
                break;

            case CombatPhase.Attacking:
                if (IsInComboWindow() && _comboIndex < 4)
                {
                    _comboBuffered = false;
                    AdvanceCombo();
                }
                else if (!_comboBuffered)
                {
                    BufferInput();
                }
                break;
        }
    }

    public void UpdateAttack()
    {
        if (_animator == null) return;

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        bool inAttack = IsAttackState(state);

        // 1. Phase 同步：Animator 不在任何 Attack 状态 → 攻击必然结束
        //    但刚出招的 0.1 秒内 Animator 还没切进 Attack（SetTrigger 要到下一帧才生效），
        //    此时读到的仍是旧状态，不能当成"攻击结束"，否则移动输入不归零会一路平移、连击也推不动
        bool justStarted = Time.time - _attackStartTime < 0.15f;
        if (_phase == CombatPhase.Attacking && !inAttack && !justStarted)
        {
            if (_comboBuffered)
            {
                _comboBuffered = false;
                StartAttack(1);
            }
            else
            {
                EndAttack();
            }
        }

        // 2. 缓冲计时衰减
        if (_comboBuffered)
        {
            _bufferTimer -= Time.deltaTime;
            if (_bufferTimer <= 0f) _comboBuffered = false;
        }
    }

    // ==================== SMB 回调（由 AttackStateBehaviour 调用）====
    // SMB 只用来更新 _comboIndex（当前 Animator 正在播第几段），
    // Phase 的 Idle/Attacking 切换统一由 UpdateAttack 每帧维护，更稳。

    public void NotifyAttackEnter(int stage)
    {
        _comboIndex = stage;
        _phase = CombatPhase.Attacking;
    }

    // ==================== 状态切换 ====================

    private void StartAttack(int stage)
    {
        _comboIndex = stage;
        _phase = CombatPhase.Attacking;
        _comboBuffered = false;
        _attackStartTime = Time.time;
        if (_animator != null) _animator.SetTrigger(AttackTriggerHash);
    }

    private void AdvanceCombo()
    {
        _comboIndex++;
        _attackStartTime = Time.time;
        if (_animator != null) _animator.SetTrigger(AttackTriggerHash);
    }

    private void EndAttack()
    {
        _phase = CombatPhase.Idle;
        _comboIndex = 0;
        _comboBuffered = false;
    }

    // ==================== 辅助 ====================

    private void BufferInput()
    {
        _comboBuffered = true;
        _bufferTimer = inputBufferTime;
    }

    private bool IsInComboWindow()
    {
        if (_animator == null) return false;
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        if (!IsAttackState(state)) return false;
        float nt = state.normalizedTime % 1f;
        return nt >= comboWindowStart;
    }

    /// <summary>软锁定：转向锁定半径内、前方扇形里最近的敌人</summary>
    private void FaceNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, lockRadius, _enemyBuffer, enemyMask);

        Transform best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            // 已死亡的敌人（死亡动画/尸体停留期间）不再作为锁定目标
            EnemyBrain brain = _enemyBuffer[i].GetComponentInParent<EnemyBrain>();
            if (brain != null && brain.IsDead) continue;

            Vector3 dir = _enemyBuffer[i].transform.position - transform.position;
            dir.y = 0f;
            float sqr = dir.sqrMagnitude;
            if (sqr < 0.0001f) continue;
            if (Vector3.Angle(transform.forward, dir) > lockAngle * 0.5f) continue;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = _enemyBuffer[i].transform;
            }
        }

        if (best == null) return;

        Vector3 look = best.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(look);
    }

    private static bool IsAttackState(AnimatorStateInfo state)
    {
        return state.IsName("Attack1") || state.IsName("Attack2")
            || state.IsName("Attack3") || state.IsName("Attack4");
    }

    // ==================== 动画事件接收（Ellen 官方动画自带）====
    // Ellen 四段 Combo 动画里自带了 MeleeAttackStart / MeleeAttackEnd 事件。
    // 只需要方法存在就能消掉 Console 的 "has no receiver" 警告。
    // 当前命中判定由 AttackHitDetection 通过 OverlapSphere 独立完成，
    // 这两个事件暂时不做任何逻辑处理，留空即可。

    public void MeleeAttackStart() { }
    public void MeleeAttackEnd() { }
}
