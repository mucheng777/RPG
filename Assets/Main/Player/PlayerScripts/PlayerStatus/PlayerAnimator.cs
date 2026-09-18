//主要是动画，基础状态混合树，起步，停步
using UnityEngine;
using System.Collections;

public class PlayerAnimator : MonoBehaviour
{
    private Animator _animator;
    private PlayerLocomotion _locomotion;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsStartingHash = Animator.StringToHash("IsStarting");
    private static readonly int IsStoppingHash = Animator.StringToHash("IsStopping");
    private static readonly int StopTypeHash = Animator.StringToHash("StopType");

    // 动画时长（你可以在 Inspector 或这里手动填）
    // 不知道的话代码会自动从 AnimatorStateInfo 读
    private float _walkStartDuration = 0.4f;   // Walk_Start 动画长度，自己调
    private float _walkEndDuration = 0.5f;     // Walk_End 动画长度
    private float _runEndDuration = 0.6f;       // Run_End 动画长度

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _locomotion = GetComponent<PlayerLocomotion>();

        _locomotion.OnStartMoving += OnStartMove;
        _locomotion.OnStopMoving += OnStopMove;
        _locomotion.OnForceCancelStop += ForceCancelStop;
    }




    // ===== 由PlayerController每帧调用 =====
    //基础状态混合树
    public void UpdateAnimParams()
    {
        if (_animator == null || _locomotion == null) return;

        // 自己根据 Locomotion 状态算 Speed，不再依赖它专门提供方法
        float targetSpeed = _locomotion.IsMoving 
            ? (_locomotion.IsSprinting ? _locomotion.SprintSpeed : _locomotion.WalkSpeed) 
            : 0f;

        _animator.SetFloat(SpeedHash, targetSpeed, 0.15f, Time.deltaTime);
    }








    // ===== 起步 =======================
    public void OnStartMove()
    {
        if (_animator == null) return;

        StopAllCoroutines(); // 取消之前可能还在跑的协程

        _animator.SetBool(IsStartingHash, true);
        _animator.SetBool(IsStoppingHash, false); // 确保停步状态关掉

        // 协程：等 Walk_Start 播完，自动翻转回 Locomotion
        StartCoroutine(StartMoveRoutine());
    }

    private IEnumerator StartMoveRoutine()
    {
        // 等动画播到 90%
        yield return new WaitForSeconds(_walkStartDuration * 0.9f);

        // 翻回 false → 状态机跳回 Locomotion
        _animator.SetBool(IsStartingHash, false);
    }
    // ======================================









    // ===== 停步 ============================
    public void OnStopMove(bool wasSprinting)
    {
        if (_animator == null) return;

        StopAllCoroutines();

        // 加锁
        _locomotion?.SetStopAnimating(true);

        // Speed 立刻归零
        _animator.SetFloat(SpeedHash, 0f);

        // 设停步参数
        _animator.SetBool(IsStoppingHash, true);
        _animator.SetBool(IsStartingHash, false); // 确保起步状态关掉
        _animator.SetInteger(StopTypeHash, wasSprinting ? 1 : 0);

        // 协程：等停步动画播完，自动翻转回 Locomotion
        float duration = wasSprinting ? _runEndDuration : _walkEndDuration;
        StartCoroutine(StopMoveRoutine(duration));
    }

    private IEnumerator StopMoveRoutine(float duration)
    {
        // 等动画播到 90%
        yield return new WaitForSeconds(duration * 0.9f);

        // 翻回 false → 状态机跳回 Locomotion
        _animator.SetBool(IsStoppingHash, false);

        // 解锁移动
        _locomotion?.SetStopAnimating(false);
    }

    // ===== 强制取消停步（停步中推摇杆时调用）=====
    public void ForceCancelStop()
    {
        StopAllCoroutines();
        _animator.SetBool(IsStoppingHash, false);
        _locomotion?.SetStopAnimating(false);
    }
    // ==============================================
}