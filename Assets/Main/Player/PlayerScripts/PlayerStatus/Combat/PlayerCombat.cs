using UnityEngine;

/// <summary>
/// 二段攻击战斗逻辑（正经版：SMB 回调驱动 + 状态机自动过渡）。
///
/// ===== 设计原则 =====
/// 1. 动画状态的"进入/退出"时机 100% 由 Animator 通过 StateMachineBehaviour 回调，
///    代码不在 Update 里轮询 normalizedTime 判断"播没播完"。
/// 2. 状态机自己处理过渡：
///      Attack1 ──ExitTime=1──► Attack1End
///      Attack2 ──ExitTime=1──► Attack2End
///      Attack1End ──ExitTime=1──► Empty
///      Attack2End ──ExitTime=1──► Empty
///    代码只负责发 AttackTrigger（连击/起手）和 MoveTrigger（中断收刀）。
/// 3. 连击窗口（"攻击动画播到一定进度后可取消"）可以用 normalizedTime，
///    因为这是"能不能取消"的判断，不是"动画结没结束"的判断。
///
/// ===== SMB 回调时序 =====
///   Attack1.Enter  → NotifyAttackEnter(1)
///   Attack1.Exit   → NotifyAttackExit()   → _phase=Recovery（状态机自动进 Attack1End）
///   Attack1End.Enter → NotifyEndEnter()
///   Attack1End.Exit  → NotifyEndExit()    → 有缓冲起手 / 否则 Idle+解锁
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    private enum CombatPhase { Idle, Attacking, Recovery }

    private PlayerLocomotion _locomotion;
    private Animator _animator;
    private PlayerController _controller;

    [Header("连击窗口")]
    [Tooltip("攻击动画播到此进度后允许取消进入下一段（normalizedTime）")]
    [SerializeField] private float comboWindowStart = 0.4f;

    [Header("输入缓冲")]
    [Tooltip("缓冲输入有效时长（秒），超时丢弃")]
    [SerializeField] private float inputBufferTime = 0.15f;

    private static readonly int AttackTriggerHash = Animator.StringToHash("AttackTrigger");
    private static readonly int MoveTriggerHash = Animator.StringToHash("MoveTrigger");

    private CombatPhase _phase = CombatPhase.Idle;
    private int _comboIndex;        // 1 = 第一段, 2 = 第二段
    private bool _comboBuffered;
    private float _bufferTimer;

    public bool IsAttacking => _phase != CombatPhase.Idle;

    void Awake()
    {
        _locomotion = GetComponent<PlayerLocomotion>();
        _animator = GetComponentInChildren<Animator>();
        _controller = GetComponent<PlayerController>();
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
                // 在连击窗口内且还没到第二段 → 立即推进
                if (IsInComboWindow() && _comboIndex < 2)
                {
                    _comboBuffered = false;
                    AdvanceCombo();
                }
                else if (!_comboBuffered)
                {
                    // 窗口外 → 缓存一次（已有缓冲则忽略，不重置计时）
                    BufferInput();
                }
                break;

            case CombatPhase.Recovery:
                // 收刀阶段也允许缓冲一次，收刀结束后起手
                if (!_comboBuffered)
                    BufferInput();
                break;
        }
    }

    public void UpdateAttack()
    {
        if (_animator == null) return;

        // 1. 缓冲计时衰减
        if (_comboBuffered)
        {
            _bufferTimer -= Time.deltaTime;
            if (_bufferTimer <= 0f) _comboBuffered = false;
        }

        // 2. 收刀阶段检测移动意图 → 中断收刀
        if (_phase == CombatPhase.Recovery && HasMoveIntent())
        {
            _animator.SetTrigger(MoveTriggerHash);
            // 收刀状态收到 MoveTrigger 后会自动过渡到 Empty，
            // EndStateBehaviour.OnStateExit 会回调 NotifyEndExit → EndAttack
        }
    }

    // ==================== SMB 回调（由 AttackStateBehaviour / EndStateBehaviour 调用）====

    /// <summary>进入攻击段时回调</summary>
    public void NotifyAttackEnter(int stage)
    {
        _comboIndex = stage;
        _phase = CombatPhase.Attacking;
        _locomotion?.SetStopAnimating(true);
    }

    /// <summary>攻击段退出时回调（状态机自动过渡到 End）</summary>
    public void NotifyAttackExit()
    {
        _phase = CombatPhase.Recovery;
        // 注意：不解锁移动！收刀期间继续锁定，直到移动中断或收刀播完
    }

    /// <summary>进入收刀时回调</summary>
    public void NotifyEndEnter()
    {
        _phase = CombatPhase.Recovery;
    }

    /// <summary>收刀退出时回调（播完 或 MoveTrigger 中断）</summary>
    public void NotifyEndExit()
    {
        if (_comboBuffered)
        {
            // 收刀结束后有缓冲输入 → 立即起手
            _comboBuffered = false;
            StartAttack(1);
        }
        else
        {
            EndAttack();
        }
    }

    // ==================== 状态切换 ====================

    private void StartAttack(int stage)
    {
        _comboIndex = stage;
        _phase = CombatPhase.Attacking;
        _comboBuffered = false;
        _animator.SetTrigger(AttackTriggerHash);
        _locomotion?.SetStopAnimating(true);
    }

    private void AdvanceCombo()
    {
        _comboIndex++;
        _animator.SetTrigger(AttackTriggerHash);
        _locomotion?.SetStopAnimating(true);
    }

    private void EndAttack()
    {
        _phase = CombatPhase.Idle;
        _comboIndex = 0;
        _comboBuffered = false;
        _animator.ResetTrigger(MoveTriggerHash);
        _locomotion?.SetStopAnimating(false);
    }

    // ==================== 辅助 ====================

    private void BufferInput()
    {
        _comboBuffered = true;
        _bufferTimer = inputBufferTime;
    }

    private bool HasMoveIntent()
    {
        return _controller != null && _controller.MoveIntent.sqrMagnitude > 0.01f;
    }

    private bool IsInComboWindow()
    {
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(1);
        if (!state.IsName("Attack1") && !state.IsName("Attack2")) return false;
        float nt = state.normalizedTime % 1f;
        return nt >= comboWindowStart;
    }
}
