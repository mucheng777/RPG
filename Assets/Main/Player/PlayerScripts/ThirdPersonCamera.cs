using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public GameObject CameraTarget;

    public float lookSpeed = 100f;//灵敏度

    [Header("俯仰角限制")]
    public float TopClamp = 70.0f; //俯仰角最大能抬多高（往上看到天）
    public float BottomClamp = -30.0f; //俯仰角最大能压多低（往下看地面）

    [Header("旋转阻尼")]
    public float yawSmoothTime = 0.12f;      // yaw平滑时间（越大越"绵"，0.08~0.15跟手，0.2+电影感）
    public float pitchSmoothTime = 0.12f;    // pitch平滑时间（可和yaw一样或略大）
    public float inputSmoothTime = 0.03f;    // 输入平滑时间（鼠标/摇杆本身的惯性，越小越跟手）

    [Header("自动回正设置")]
    public bool enableAutoCenter = true;          // 是否启用挂机自动回正
    public float idleTimeToCenter = 3.0f;         // 挂机多久后开始回正（秒）
    public float defaultPitchOnCenter = 0.0f;     // 回正时Pitch恢复到的值（0=平视）
    public float centerSmoothTime = 0.3f;         // 回正过程的平滑时间（越小回正越快，0.3左右较自然）
    public Transform playerTransform;             // 可选：指定角色，回正Yaw会回到角色面朝方向（不挂则回初始角度）

    // 自动回正专用变量
    private float _idleTimer = 0f;
    private float _initialYaw;                    // 初始Yaw（用于无角色引用时回正）
    private float _centerYawVelocity;             // 回正时Yaw的内部速度
    private float _centerPitchVelocity;           // 回正时Pitch的内部速度

    private const float _threshold = 0.01f; //移动量小于这个值当没动
    private float _cinemachineTargetYaw; //当前相机目标的水平偏航角（左右转）
    private float _cinemachineTargetPitch; //当前相机目标的垂直俯仰角（上下看）

    private float _smoothYaw;               // 实际显示的yaw（平滑后）
    private float _smoothPitch;             // 实际显示的pitch（平滑后）
    private float _yawVelocity;             // SmoothDampAngle内部速度（不用管，自动算）
    private float _pitchVelocity;           // 同上
    private Vector2 _lookSmooth;            // 平滑后的输入值
    private Vector2 _lookVelocity;          // 输入平滑的内部速度

    void Start()
    {
        _cinemachineTargetYaw = CameraTarget.transform.rotation.eulerAngles.y;//把相机目标的初始水平朝向设为 CameraTarget 当前在场景里的 y 旋转值。这样游戏开始时相机不会突然跳到 0 度
        _initialYaw = _cinemachineTargetYaw; // 记录初始角度作为回正基准
    }

    private void Update()
    {
        bool hasInput = _look.sqrMagnitude >= _threshold;

        if (hasInput)
        {
            // ===== 有输入：正常处理，并打断回正 =====
            _idleTimer = 0f;
            _centerYawVelocity = 0f;
            _centerPitchVelocity = 0f;

            _lookSmooth.x = Mathf.SmoothDamp(_lookSmooth.x, _look.x, ref _lookVelocity.x, inputSmoothTime);
            _lookSmooth.y = Mathf.SmoothDamp(_lookSmooth.y, _look.y, ref _lookVelocity.y, inputSmoothTime);

            _cinemachineTargetYaw += _lookSmooth.x * Time.deltaTime * lookSpeed;
            _cinemachineTargetPitch -= _lookSmooth.y * Time.deltaTime * lookSpeed;

            _look = Vector2.zero;
        }
        else
        {
            // ===== 无输入：输入平滑归零，检测闲置回正 =====
            _lookSmooth.x = Mathf.SmoothDamp(_lookSmooth.x, 0f, ref _lookVelocity.x, inputSmoothTime);
            _lookSmooth.y = Mathf.SmoothDamp(_lookSmooth.y, 0f, ref _lookVelocity.y, inputSmoothTime);

            if (enableAutoCenter)
            {
                _idleTimer += Time.deltaTime;
                if (_idleTimer > idleTimeToCenter)
                {
                    // 确定回正目标角度
                    float targetYaw = _initialYaw;
                    if (playerTransform != null)
                    {
                        // 如果挂了角色，就回正到角色当前的面朝方向（背后视角）
                        targetYaw = playerTransform.eulerAngles.y;
                    }
                    float targetPitch = defaultPitchOnCenter;

                    // 用 SmoothDampAngle 将目标角度慢慢拉回正位
                    _cinemachineTargetYaw = Mathf.SmoothDampAngle(_cinemachineTargetYaw, targetYaw, ref _centerYawVelocity, centerSmoothTime);
                    _cinemachineTargetPitch = Mathf.SmoothDampAngle(_cinemachineTargetPitch, targetPitch, ref _centerPitchVelocity, centerSmoothTime);
                }
            }
        }

        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        // 角度平滑：目标角度 → 实际角度
        _smoothYaw = Mathf.SmoothDampAngle(_smoothYaw, _cinemachineTargetYaw, ref _yawVelocity, yawSmoothTime);
        _smoothPitch = Mathf.SmoothDampAngle(_smoothPitch, _cinemachineTargetPitch, ref _pitchVelocity, pitchSmoothTime);

        CameraTarget.transform.rotation = Quaternion.Euler(_smoothPitch, _smoothYaw, 0.0f);
    }

    //角度规范化
    private static float ClampAngle(float lfAngle,float lfMin,float lfMax)
    {
        if(lfAngle < -360f)lfAngle += 360f;
        if(lfAngle > 360f)lfAngle -= 360f;
        return Mathf.Clamp(lfAngle,lfMin,lfMax);
    }

    //输入回调，InputSystem的回调方法，把输入值存到_look里面然后放到Update里面用
    private Vector2 _look;
    public void OnLook(InputValue value)
    {
        _look = value.Get<Vector2>();
    }


}
