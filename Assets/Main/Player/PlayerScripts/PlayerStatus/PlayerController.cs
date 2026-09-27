using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PlayerLocomotion _locomotion;
    public PlayerAnimator _animator;
    public PlayerCombat _combat;

    //引用PlayerControls类，也就是输入系统
    private PlayerControls input;

    void Awake()
    {
        _locomotion = GetComponent<PlayerLocomotion>();
        _animator = GetComponent<PlayerAnimator>();
        _combat = GetComponent<PlayerCombat>();
        input = new PlayerControls();
    }

    void OnEnable()
    {
        input.Player.Enable();
    }

    void OnDisable()
    {
        input.Player.Disable();
    }

    void Update()
    {
        // 攻击期间锁定移动输入，避免 ProcessStopAnimationLock 自动解锁
        Vector2 move = (_combat != null && _combat.IsAttacking) ? Vector2.zero : input.Player.Move.ReadValue<Vector2>();
        _locomotion.SetMoveInput(move);

        float sprint = input.Player.Sprint.ReadValue<float>();
        _locomotion.SetSprint(sprint > 0.5f);

        _locomotion.UpdateLocomotion();
        _animator.UpdateAnimParams();

        // 攻击输入与连击更新
        if (input.Player.Fire.WasPressedThisFrame())
            _combat?.TryAttack();
        _combat?.UpdateAttack();
    }
}