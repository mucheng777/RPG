using UnityEngine;

/// <summary>
/// 根运动接管（OnAnimatorMove）。
///
/// 动画位移的分拣规则（在动画剪辑 Inspector → Animation 页签设置，代码不管）：
///   奔跑 / 待机 / 受击 → Root Transform Position (XZ) 勾 Bake Into Pose（位移归代码）
///   攻击 / 翻滚 / 冲刺 → 不勾（位移由动画输出）
///
/// 本脚本只把 Attack1~4 状态的位移交给动画，
/// 其他状态的 deltaPosition 直接丢弃，移动权归还 PlayerLocomotion。
///
/// 注意：必须挂在 Animator 所在的那一个 GameObject 上，否则 OnAnimatorMove 不会被调用。
/// </summary>
[RequireComponent(typeof(Animator))]
public class RootMotionController : MonoBehaviour
{
    [Tooltip("攻击状态所在的 Animator 层索引（你的 Attack 层是第 1 层）")]
    [SerializeField] private int attackLayerIndex = 1;

    private Animator _animator;
    private CharacterController _controller;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        // CharacterController 通常挂在角色根物体，Animator 可能在子物体上
        _controller = GetComponentInParent<CharacterController>();
    }

    void OnAnimatorMove()
    {
        // 只有攻击类状态吃动画位移；其余状态直接丢弃 = 位移归代码
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        bool isAttack = state.IsName("Attack1") || state.IsName("Attack2")
                     || state.IsName("Attack3") || state.IsName("Attack4");
        if (!isAttack) return;

        Vector3 delta = _animator.deltaPosition;
        if (_controller != null)
            _controller.Move(delta);
        else
            transform.position += delta;

        transform.rotation *= _animator.deltaRotation;
    }
}
