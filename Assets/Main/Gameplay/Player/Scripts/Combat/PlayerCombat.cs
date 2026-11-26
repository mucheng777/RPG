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
    [SerializeField] private float comboWindowStart = 0.4f;

    [Header("输入缓冲")]
    [Tooltip("缓冲输入有效时长（秒），超时丢弃")]
    [SerializeField] private float inputBufferTime = 0.15f;

    [Tooltip("Animator 攻击状态所在的 Layer 索引（和 AttackHitDetection 保持一致）")]
    [SerializeField] private int attackLayerIndex = 1;

    private static readonly int AttackTriggerHash = Animator.StringToHash("AttackTrigger");

    private Animator _animator;

    private CombatPhase _phase = CombatPhase.Idle;
    private int _comboIndex;   // 当前段 1~4，0=未开始
    private bool _comboBuffered;
    private float _bufferTimer;

    public bool IsAttacking => _phase != CombatPhase.Idle;

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
    }

    // ==================== 对外入口 ====================

    public void TryAttack()
    {
        if (_animator == null) return;

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
        if (_phase == CombatPhase.Attacking && !inAttack)
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
        if (_animator != null) _animator.SetTrigger(AttackTriggerHash);
    }

    private void AdvanceCombo()
    {
        _comboIndex++;
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
