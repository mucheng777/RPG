using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PlayerLocomotion _locomotion;
    public PlayerAnimator _animator;

    void Awake()
    {
        _locomotion = GetComponent<PlayerLocomotion>();
        _animator = GetComponent<PlayerAnimator>();
    }

    //WASD输入
    void OnMove(InputValue value)
    {
        _locomotion.SetMoveInput(value.Get<Vector2>());
    }

    //加速左Shift按钮
    void OnSprint(InputValue value)
    {
        _locomotion.SetSprint(value.Get<float>() > 0.5f);
    }

    void Update()
    {
        _locomotion.UpdateLocomotion();
        _animator.UpdateAnimParams();
    }
}