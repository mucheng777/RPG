using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PlayerLocomotion _locomotion;
    public PlayerAnimator _animator;

    //引用PlayerControls类，也就是输入系统
    private PlayerControls input;

    void Awake()
    {
        _locomotion = GetComponent<PlayerLocomotion>();
        _animator = GetComponent<PlayerAnimator>();
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
        // 每帧直接读值，没输入就是 Vector2.zero / 0
        Vector2 move = input.Player.Move.ReadValue<Vector2>();
        _locomotion.SetMoveInput(move);

        float sprint = input.Player.Sprint.ReadValue<float>();
        _locomotion.SetSprint(sprint > 0.5f);

        _locomotion.UpdateLocomotion();
        _animator.UpdateAnimParams();
    }
}