using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 角色移动控制（极简版）。
/// 起步/停步动画过渡已从 Animator Controller 删除，
/// 所以 OnStartMoving/OnStopMoving 事件、停步锁、SetStopAnimating 全部移除。
/// </summary>
public class PlayerLocomotion : MonoBehaviour
{
    private CharacterController _controller;
    private Transform _cameraTransform;

    [Header("移动")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 6f;
    public float rotationSmoothTime = 0.15f;
    public float gravity = -9.81f;

    private Vector2 _moveInput;
    private bool _isSprinting;

    private float _currentMaxSpeed;
    private float _targetRot;
    private float _rotationVelocity;
    private float _verticalVelocity;
    private float _cachedCameraYaw;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (_cameraTransform == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null)
                _cameraTransform = mainCam.transform;
        }
    }

    public void SetMoveInput(Vector2 input) => _moveInput = input;
    public void SetSprint(bool value) => _isSprinting = value;

    public bool IsMoving => _moveInput != Vector2.zero;
    public bool IsSprinting => _isSprinting;
    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;

    public void UpdateLocomotion()
    {
        CacheCameraYaw();
        GroundCheck();
        ApplyGravity();
        UpdateTargetSpeed();

        Vector3 finalMove = Vector3.zero;

        if (IsMoving)
        {
            Rotate();
            finalMove += GetHorizontalMove();
        }
        else
        {
            _targetRot = transform.eulerAngles.y;
        }

        finalMove.y = _verticalVelocity;

        _controller.Move(finalMove * Time.deltaTime);
    }

    private void CacheCameraYaw()
    {
        if (_cameraTransform != null)
            _cachedCameraYaw = _cameraTransform.eulerAngles.y;
        else
            _cachedCameraYaw = transform.eulerAngles.y;
    }

    private void UpdateTargetSpeed()
    {
        _currentMaxSpeed = _isSprinting ? sprintSpeed : walkSpeed;
    }

    private Vector3 GetHorizontalMove()
    {
        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
        Vector3 moveDir = Quaternion.Euler(0f, _cachedCameraYaw, 0f) * inputDir;
        return moveDir * _currentMaxSpeed;
    }

    void Rotate()
    {
        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
        _targetRot = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + _cachedCameraYaw;
        float rot = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRot, ref _rotationVelocity, rotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, rot, 0f);
    }

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
