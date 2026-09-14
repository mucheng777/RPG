using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLocomotion : MonoBehaviour
{
    private CharacterController _controller;
    private Transform _cameraTransform;
    private PlayerAnimator _playerAnimator;

    [Header("移动")]
    public float walkSpeed = 3f;
    public float sprintSpeed = 6f;
    public float rotationSmoothTime = 0.15f;
    public float gravity = -9.81f;

    // 输入
    private Vector2 _moveInput;
    private bool _isSprinting;

    // 移动计算
    private float _currentMaxSpeed;
    private float _targetRot;
    private float _rotationVelocity;
    private float _verticalVelocity;
    private float _cachedCameraYaw;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _cameraTransform = Camera.main.transform;
        _playerAnimator = GetComponent<PlayerAnimator>();
    }

    // ===== 主脚本PlayerController调用 =====
    public void SetMoveInput(Vector2 input) => _moveInput = input;
    public void SetSprint(bool value) => _isSprinting = value;

    public void UpdateLocomotion()
    {
        // 缓存相机朝向
        if (_cameraTransform != null)
            _cachedCameraYaw = _cameraTransform.eulerAngles.y;

        GroundCheck();
        ApplyGravity();
        _currentMaxSpeed = _isSprinting ? sprintSpeed : walkSpeed;

        if (_moveInput != Vector2.zero)
        {
            Rotate();
            Move();
        }
        else
        {
            _targetRot = transform.eulerAngles.y;
        }

        // 重力
        _controller.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
    }
    // ===== 主脚本PlayerController调用 =====

    // ===== 给 PlayerAnimator 调用 =====
    public float GetCurrentSpeed()
    {
        return _moveInput != Vector2.zero ? _currentMaxSpeed : 0f;
    }
    // ===== 给 PlayerAnimator 调用 =====










    void Rotate()
    {
        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
        _targetRot = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + _cachedCameraYaw;

        float rot = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            _targetRot,
            ref _rotationVelocity,
            rotationSmoothTime
        );
        transform.rotation = Quaternion.Euler(0f, rot, 0f);
    }

    // ===== 移动 =====
    void Move()
    {
        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
        Vector3 moveDir = Quaternion.Euler(0f, _cachedCameraYaw, 0f) * inputDir;
        Vector3 velocity = moveDir * _currentMaxSpeed;
        velocity.y = _verticalVelocity;
        _controller.Move(velocity * Time.deltaTime);
    }

    // ===== 物理 =====
    void GroundCheck()
    {
        if (_controller.isGrounded && _verticalVelocity < 0)
            _verticalVelocity = -2f;
    }

    void ApplyGravity()
    {
        _verticalVelocity += gravity * Time.deltaTime;
    }
}