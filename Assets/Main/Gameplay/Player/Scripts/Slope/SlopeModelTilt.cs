//消费层
using UnityEngine;

/// <summary>
/// 斜面姿态对齐（消费方脚本，可选）：读取 ISlopeDataProvider 的地面法线，
/// 让人物视觉模型平滑贴合斜面坡度。
///
/// 使用要求：
/// - 挂在 Player 根物体上（与 SlopeDetector 同物体），model 指向视觉模型子物体；
/// - 根物体始终保持竖直（由 PlayerLocomotion 管理旋转与碰撞），本脚本只倾斜 model；
/// - 在 LateUpdate 中执行（位于所有 Update 移动逻辑之后），不会被 PlayerLocomotion
///   的 Rotate() 覆盖，因为它作用的对象是模型子物体而非根物体。
///
/// 行为：IsOnSlope 与否无需区分——平地 / 空中时 GroundNormal 为 up，
/// 目标姿态自动等于初始姿态，模型自然回正。
/// </summary>
[DefaultExecutionOrder(100)]
[AddComponentMenu("RPG/Slope Model Tilt")]
public class SlopeModelTilt : MonoBehaviour
{
    [Tooltip("人物视觉模型（mesh / Animator 所在子物体），不能是挂着 CharacterController 的根物体")]
    [SerializeField] private Transform model;

    [Tooltip("对齐速度：越大贴合越快")]
    [SerializeField] private float alignSpeed = 10f;

    [Tooltip("最大倾斜角（度），防止在陡坡上过度翻转")]
    [SerializeField] private float maxTiltAngle = 60f;

    private ISlopeDataProvider _provider;
    private Quaternion _initialLocalRotation;   // 模型自身初始偏移（如 yaw 修正）会被保留

    private void Awake()
    {
        _provider = GetComponent<ISlopeDataProvider>();//使用接口的地方
        if (_provider == null) _provider = GetComponentInParent<ISlopeDataProvider>();

        if (model == null)
        {
            if (transform.childCount > 0) model = transform.GetChild(0);
            if (model != null)
                Debug.LogWarning("[SlopeModelTilt] 未指定 model，已自动使用第一个子物体：" + model.name, this);
        }

        if (model != null)
            _initialLocalRotation = model.localRotation;
    }

    private void LateUpdate()
    {
        if (_provider == null || model == null) return;

        Vector3 normal = _provider.GroundNormal;

        // 限制最大倾斜角：把法线往 up 方向拉回
        float groundAngle = Vector3.Angle(Vector3.up, normal);
        if (groundAngle > maxTiltAngle)
            normal = Vector3.Slerp(Vector3.up, normal, maxTiltAngle / groundAngle);

        // 目标姿态 = 根朝向整体贴合斜面：up 对齐法线，前向保持原朝向（最小旋转，无扭转）
        Quaternion targetWorld = Quaternion.FromToRotation(Vector3.up, normal)
                                 * transform.rotation
                                 * _initialLocalRotation;

        // 帧率无关的平滑过渡
        float t = 1f - Mathf.Exp(-alignSpeed * Time.deltaTime);
        model.rotation = Quaternion.Slerp(model.rotation, targetWorld, t);
    }
}
