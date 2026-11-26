using UnityEngine;

/// <summary>
/// 极简版：只负责往 Animator 推 Speed 参数。
/// 起步/停步/收刀等过渡动画已全部从 Animator Controller 删除，
/// PlayerAnimator 不再需要订阅任何事件或管理协程。
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    private Animator _animator;
    private PlayerLocomotion _locomotion;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _locomotion = GetComponent<PlayerLocomotion>();
    }

    // 由 PlayerController 每帧调用
    public void UpdateAnimParams()
    {
        if (_animator == null || _locomotion == null) return;

        float targetSpeed = _locomotion.IsMoving
            ? (_locomotion.IsSprinting ? _locomotion.SprintSpeed : _locomotion.WalkSpeed)
            : 0f;

        // dampTime=0.15s 让 Speed 平滑过渡，避免走路/跑步切换时动画跳变
        _animator.SetFloat(SpeedHash, targetSpeed, 0.15f, Time.deltaTime);
    }
}
