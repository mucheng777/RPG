//总控脚本
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("模块引用")]
    public PlayerLocomotion _locomotion;
    public PlayerAnimator _animator;

    void Awake()
    {
        _locomotion = GetComponent<PlayerLocomotion>();
        _animator = GetComponent<PlayerAnimator>();
    }

    //对应InputSystem的WASD移动
    void OnMove(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        _locomotion.SetMoveInput(input);    //来自PlayerLocomotion.cs
    }

    //对应的InputSystem的左Shift加速
    void OnSprint(InputValue value)
    {
        _locomotion.SetSprint(value.Get<float>() > 0.5f);  //来自PlayerLocomotion.cs
    }

    void Update()
    {
        _locomotion.UpdateLocomotion();     //来自PlayerLocomotion.cs
        _animator.UpdateAnimParams();       //来自PlayerAnimator.cs
    }
}