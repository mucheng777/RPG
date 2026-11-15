using UnityEngine;
using UnityEngine.InputSystem;

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

    private bool _wasMoving;
    private bool _wasSprinting;
    private bool _isInStopAnimation;

    // ===== 事件声明 =====
    public event System.Action OnStartMoving;       //开始移动事件
    public event System.Action<bool> OnStopMoving;  //停止移动事件
    public event System.Action OnForceCancelStop;   //强制取消停止动画事件

    void Awake()
    {
        _controller = GetComponent<CharacterController>();

        // 如果 Inspector 里没拖相机，就自动找主相机
        if (_cameraTransform == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null)
                _cameraTransform = mainCam.transform;
        }
    }

    public void SetStopAnimating(bool value) => _isInStopAnimation = value;
    public void SetMoveInput(Vector2 input) => _moveInput = input;
    public void SetSprint(bool value) => _isSprinting = value;

    public bool IsMoving => _moveInput != Vector2.zero;
    public bool IsSprinting => _isSprinting;
    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;

    //======================主逻辑================================
    public void UpdateLocomotion()
    {
        CacheCameraYaw();
        GroundCheck();
        ApplyGravity();

        // 停步锁
        if (ProcessStopAnimationLock())
            return;

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

        UpdateMoveStateAndNotify();
    }
    //======================================================================

    /// <summary>
    /// 直接拿相机的 Y 轴旋转当 Yaw，简单够用
    /// </summary>
    private void CacheCameraYaw()
    {
        if (_cameraTransform != null)
            _cachedCameraYaw = _cameraTransform.eulerAngles.y;
        else
            _cachedCameraYaw = transform.eulerAngles.y; // fallback 到自己
    }

    private bool ProcessStopAnimationLock()
    {
        if (!_isInStopAnimation) return false;

        if (!_wasMoving && IsMoving)
        {
            _isInStopAnimation = false;
            OnForceCancelStop?.Invoke();
            OnStartMoving?.Invoke();
            _wasMoving = true;
            _wasSprinting = _isSprinting;
            return false;
        }
        else
        {
            _moveInput = Vector2.zero;
            _currentMaxSpeed = 0f;
            _targetRot = transform.eulerAngles.y;

            // 停步锁定期间只处理重力
            _controller.Move(Vector3.up * _verticalVelocity * Time.deltaTime);

            _wasMoving = false;
            _wasSprinting = false;
            return true;
        }
    }

    private void UpdateTargetSpeed()
    {
        _currentMaxSpeed = _isSprinting ? sprintSpeed : walkSpeed;
    }

    /// <summary>
    /// 只计算水平移动向量，不调用 Move
    /// </summary>
    private Vector3 GetHorizontalMove()
    {
        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
        Vector3 moveDir = Quaternion.Euler(0f, _cachedCameraYaw, 0f) * inputDir;
        return moveDir * _currentMaxSpeed;
    }


    private void UpdateMoveStateAndNotify()
    {
        bool isMovingNow = IsMoving;

        if (!_wasMoving && isMovingNow)
        {
            OnStartMoving?.Invoke();
        }
        else if (_wasMoving && !isMovingNow)
        {
            OnStopMoving?.Invoke(_wasSprinting);
        }

        _wasMoving = isMovingNow;
        _wasSprinting = _isSprinting;
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