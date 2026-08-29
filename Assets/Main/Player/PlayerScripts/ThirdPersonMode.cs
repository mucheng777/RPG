using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;   // 如果不使用数学库也可以删掉
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonMode : MonoBehaviour
{
    private CharacterController _controller;
    private GameObject _mainCamera;
    private Animator _animator;

    [Header("移动设置")]
    public float walkSpeed = 4.0f;//行走速度
    public float sprintSpeed = 8.0f;//奔跑速度
    private float _currentMaxSpeed; //当前最大速度

    [Header("旋转平滑过渡")]
    float _targetRot = 0.0f;
    public float RotationSmoothTime = 0.01f;
    float _rotationVelocity;

    [Header("跳跃设置")]
    public float jumpHeight = 1.5f;      // 跳跃高度
    public float gravity = -9.81f;       // 重力
    private bool _isGrounded;            // 是否在地面
    private float _verticalVelocity;     // 垂直速度

    [Header("攻击设置")]
    public float attackAnimationLength = 3.0f;  // 攻击动画时长（秒）,可更改
    private float _attackTimer = 0f;
    void Start()
    {
        if(_mainCamera == null)
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }
        _controller = GetComponent<CharacterController>();    
        _animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 地面检测（简单方式：使用 CharacterController.isGrounded）
        _isGrounded = _controller.isGrounded;
        if (_isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -2f; // 轻微吸附地面
        }

        // ========== 跳跃输入 ==========
        if (Keyboard.current.spaceKey.wasPressedThisFrame && _isGrounded)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _animator.SetTrigger("Jump");   // 触发跳跃动画
        }

        // ========== 攻击开始 ==========
        if(_attackTimer>0)//计时器递减
        {
            Debug.Log($"冷却中，剩余时间：{_attackTimer}");
            _attackTimer -= Time.deltaTime;
        }
            

        if (Input.GetMouseButtonDown(0)&&_attackTimer<=0)
        {
            _animator.SetTrigger("Attack");
            _attackTimer = attackAnimationLength;//冷却时间
        }
        // ========== 攻击结束 ==========

        // ========== 移动开始 ==========
        bool isSprinting = Keyboard.current.leftShiftKey.isPressed;
        _currentMaxSpeed = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 velocity = Vector3.zero;

        if(_move != Vector2.zero)
        {
            Vector3 inputDir = new Vector3(_move.x,0.0f,_move.y).normalized;

            _targetRot = Mathf.Atan2(inputDir.x,inputDir.z)*Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;

            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y,_targetRot,ref _rotationVelocity,RotationSmoothTime);

            transform.rotation = Quaternion.Euler(0.0f,rotation,0.0f);
            Vector3 targetDir = Quaternion.Euler(0.0f,_targetRot,0.0f)*Vector3.forward;
            velocity += targetDir.normalized * _currentMaxSpeed;
        } 

        // 应用垂直速度（重力+跳跃）
        _verticalVelocity += gravity * Time.deltaTime;
        velocity.y = _verticalVelocity;

        float currentSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        //实时计算速度值currentSpeed来传给混合树的Speed
        _animator.SetFloat("Speed", currentSpeed);   // 参数名必须和混合树中的一致

        //移动玩家
        _controller.Move(velocity * Time.deltaTime);
        // ========== 移动结束 ==========
    }


    //移动输入函数
    private Vector2 _move;
    void OnMove(InputValue value)
    {
        _move = value.Get<Vector2>();
    }
}
