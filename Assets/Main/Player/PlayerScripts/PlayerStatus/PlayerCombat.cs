using UnityEngine;

/// <summary>
/// 主要是管角色攻击连击状态
/// 对应了Attack Layer
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    private PlayerController _playerController;
    private PlayerLocomotion _locomotion;
    private Animator _animator;

    [Header("连击窗口")]
    [Tooltip("Attack 动画中接受连击输入的 normalizedTime 起点（0~1）")]
    [SerializeField] private float comboWindowStart = 0.4f;

    [Tooltip("End 动画结束阈值，normalizedTime 超过此值视为收招完毕")]
    [SerializeField] private float endThreshold = 0.95f;

    private static readonly int AttackTriggerHash = Animator.StringToHash("AttackTrigger");

    private int _comboIndex;        // 0=空闲, 1/2=当前段
    private bool _isAttacking;
    private bool _comboBuffered;   // 玩家在非窗口期早按的缓冲

    // Attack Layer 的状态名，用于 IsName 判断
    private static readonly string[] AttackStateNames = { "Attack1", "Attack2" };
    private static readonly string[] EndStateNames   = { "Attack1End", "Attack2End"};

    public bool IsAttacking => _isAttacking;

    private float _debugTimer;

    void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _locomotion = GetComponent<PlayerLocomotion>();
        _animator = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// 由 PlayerController 在 Fire.WasPressedThisFrame() 时调用。
    /// 根据当前连击状态决定是否推进段数或缓冲输入。
    /// </summary>
    public void TryAttack()
    {
        if (_animator == null) return;

        if (!_isAttacking)
        {
            // 空闲 → 启动第一段
            StartAttack(1);
            return;
        }

        // 正在攻击：判断是否在连击窗口内
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(1);

        // 检查是否在 Attack 动画的后段（normalizedTime >= comboWindowStart）
        bool inAttackPhase = false;
        for (int i = 0; i < AttackStateNames.Length; i++)
        {
            if (state.IsName(AttackStateNames[i]))
            {
                inAttackPhase = state.normalizedTime >= comboWindowStart;
                break;
            }
        }

        // 检查是否在 End 动画中（End 全程接受连击）
        bool inEndPhase = false;
        for (int i = 0; i < EndStateNames.Length; i++)
        {
            if (state.IsName(EndStateNames[i]))
            {
                inEndPhase = true;
                break;
            }
        }

        if ((inAttackPhase || inEndPhase) && _comboIndex < 2)
        {
            // 在窗口内 → 立即推进
            AdvanceCombo();
        }
        else
        {
            // 不在窗口内 → 缓冲，等进入窗口时由 UpdateAttack 推进
            _comboBuffered = true;
        }
    }

    /// <summary>
    /// 每帧由 PlayerController 调用，检测动画进度并管理状态转换。
    /// </summary>
    public void UpdateAttack()
    {
        if (_animator == null || !_isAttacking) return;

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(1);

        // 在 End 动画中检测结束
        bool inEnd = false;
        int endStage = -1;
        for (int i = 0; i < EndStateNames.Length; i++)
        {
            if (state.IsName(EndStateNames[i]))
            {
                inEnd = true;Debug.Log("inEnd = true");
                endStage = i + 1; // 1/2
                break;
            }
        }

        if (inEnd && state.normalizedTime >= endThreshold)//判断是不是收刀动画inEnd并且收刀动画播放的时间大于endThreshold（百分比）
        {
            // End 动画播完
            if (_comboBuffered && endStage < 2)
            {
                // 有缓冲且不是最后一段 → 推进下一段
                _comboBuffered = false;
                AdvanceCombo();
            }
            else
            {
                // 无缓冲或已是最后一段 → 结束攻击
                EndAttack();
            }
            return;
        }



        // ===== 每秒打印一次 End 动画的进度 =====
        _debugTimer += Time.deltaTime;
        if (_debugTimer >= 1.0f)
        {
            _debugTimer = 0f;
            Debug.Log($"[End进度] normalizedTime = {state.normalizedTime:F3} | endThreshold = {endThreshold} | 是否超过阈值 = {state.normalizedTime >= endThreshold} | 状态机在过渡? = {_animator.IsInTransition(1)}");
        }
        // ===== 打印结束 =====一会删




        // 在 Attack 动画中且已过窗口起点 → 如果有缓冲则推进
        if (_comboBuffered)
        {
            for (int i = 0; i < AttackStateNames.Length; i++)
            {
                if (state.IsName(AttackStateNames[i]) && state.normalizedTime >= comboWindowStart && _comboIndex < 2)
                {
                    _comboBuffered = false;
                    AdvanceCombo();
                    break;
                }
            }
        }
    }

    private void StartAttack(int stage)
    {
        _comboIndex = stage;
        _isAttacking = true;
        _comboBuffered = false;
        _animator.SetTrigger(AttackTriggerHash);
        _locomotion?.SetStopAnimating(true);
    }

    private void AdvanceCombo()
    {
        _comboIndex++;
        _animator.SetTrigger(AttackTriggerHash);
    }

    private void EndAttack()
    {
        Debug.Log("=== EndAttack() 被调用了！ ===");
        _comboIndex = 0;
        _isAttacking = false;
        _comboBuffered = false;
        _locomotion?.SetStopAnimating(false);
    }
}
