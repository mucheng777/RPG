using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("目标")]
    public Transform followTarget;  // 拖 Player/Follow 空节点

    [Header("输入")]
    public PlayerControls input;   // 生成的 Input Actions C# 类实例

    [Header("相机参数")]
    public float distance = 4f;        // 离目标多远
    public float targetHeight = 1.5f;  // 看向目标的高度（胸口）
    public float cameraHeight = 1.2f;  // 相机自身高度偏移
    public float sensitivity = 0.3f;
    public float minPitch = -25f;
    public float maxPitch = 60f;

    [Header("平滑")]
    public float rotationSmoothTime = 0.05f;
    public float positionSmoothTime = 0.1f;

    [Header("弹簧臂碰撞")]
    public bool enableCollision = true;
    public float minDistance = 1f;
    public LayerMask collisionMask;

    // 内部
    private float _yaw;
    private float _pitch;
    private float _yawVel;
    private float _pitchVel;
    private Vector3 _currentPos;
    private Vector3 _posVel;
    private float _currentDist;
    private float _distVel;

    void Awake()
    {
        if (input == null) input = new PlayerControls();
        _currentPos = transform.position;
        _currentDist = distance;

        // 初始朝向：面向目标
        if (followTarget != null)
        {
            Vector3 dir = (transform.position - followTarget.position).normalized;
            _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            _pitch = 0f;
        }
    }

    void OnEnable()
    {
        input.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        input.Disable();
    }

    void LateUpdate()
    {
        if (followTarget == null) return;

        // 1. 读输入（防御性校验：输入系统在场景重载时序下可能产生 NaN/Infinity）
        Vector2 look = input.Player.Look.ReadValue<Vector2>();
        if (!IsValid(look)) look = Vector2.zero;

        // 2. 算目标角度
        float targetYaw = _yaw + look.x * sensitivity;
        float targetPitch = _pitch - look.y * sensitivity;
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        // 3. 平滑（若状态已被污染，先复位角度与速度，避免 NaN 永久传播）
        if (!IsValid(_yaw) || !IsValid(_pitch) || !IsValid(_yawVel) || !IsValid(_pitchVel))
        {
            _yaw = 0f; _pitch = 0f; _yawVel = 0f; _pitchVel = 0f;
            targetYaw = 0f; targetPitch = 0f;
        }
        _yaw = Mathf.SmoothDampAngle(_yaw, targetYaw, ref _yawVel, rotationSmoothTime);
        _pitch = Mathf.SmoothDampAngle(_pitch, targetPitch, ref _pitchVel, rotationSmoothTime);

        // 4. 计算旋转（Euler 输入必须为有限值，否则结果四元数模长≠1 触发断言）
        Quaternion rotation;
        if (IsValid(_yaw) && IsValid(_pitch))
            rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        else
            rotation = Quaternion.identity;

        // 5. 弹簧臂距离（带碰撞检测）
        float desiredDist = distance;
        if (enableCollision)
        {
            Vector3 camToTarget = -(rotation * new Vector3(0, cameraHeight, -desiredDist).normalized);
            if (Physics.Raycast(followTarget.position + Vector3.up * targetHeight,
                                 camToTarget, out RaycastHit hit, distance + 0.5f, collisionMask))
            {
                desiredDist = Mathf.Min(hit.distance - 0.3f, distance);
                desiredDist = Mathf.Max(desiredDist, minDistance);
            }
        }
        if (!IsValid(_distVel)) _distVel = 0f;
        _currentDist = Mathf.SmoothDamp(_currentDist, desiredDist, ref _distVel, 0.08f);

        // 6. 计算目标位置
        Vector3 localOffset = new Vector3(0, cameraHeight, -_currentDist);
        Vector3 targetPos = followTarget.position + Vector3.up * targetHeight + rotation * localOffset;

        // 7. 位置平滑
        if (!IsValid(_currentPos) || !IsValid(_posVel))
        {
            _currentPos = followTarget.position + Vector3.up * targetHeight + rotation * localOffset;
            _posVel = Vector3.zero;
        }
        _currentPos = Vector3.SmoothDamp(_currentPos, targetPos, ref _posVel, positionSmoothTime);

        // 8. 应用（最终防线：赋值前过滤 NaN/Infinity，杜绝向 transform 写入无效坐标）
        if (!IsValid(_currentPos))
            _currentPos = followTarget.position + Vector3.up * targetHeight - Vector3.forward * distance;
        transform.position = _currentPos;
        transform.rotation = rotation;

        // 9. 让相机看向目标（稍微偏上）
        Vector3 lookPoint = followTarget.position + Vector3.up * targetHeight;
        transform.LookAt(lookPoint);
    }

    /// <summary>
    /// 数值合法性校验：NaN 与 Infinity 均视为非法。
    /// 用于过滤输入系统异常值、平滑过程污染值，防止向 transform 写入无效坐标。
    /// </summary>
    private static bool IsValid(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static bool IsValid(Vector2 v) => IsValid(v.x) && IsValid(v.y);
    private static bool IsValid(Vector3 v) => IsValid(v.x) && IsValid(v.y) && IsValid(v.z);
}