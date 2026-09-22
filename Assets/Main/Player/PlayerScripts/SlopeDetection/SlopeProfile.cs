//配置层
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 斜面判定配置资产（ScriptableObject）。
/// 不同角色 / 不同地形可创建多份实例复用；检测器本身不持有任何数值。
/// 创建路径：Project 窗口右键 → Create → RPG → Slope Profile
/// </summary>
[CreateAssetMenu(fileName = "SlopeProfile", menuName = "RPG/Slope Profile")]
public class SlopeProfile : ScriptableObject
{
    [Header("坡度阈值（度）")]
    [Tooltip("可攀爬最大坡角，超过视为不可行走（如墙体）")]
    public float maxSlopeAngle = 45f;

    [Tooltip("最小斜坡角度，低于视为平地（过滤微小起伏）")]
    public float minSlopeAngle = 10f;

    [Header("检测参数")]
    [Tooltip("地面检测层。建议排除角色自身所在层")]
    public LayerMask groundMask = ~0;

    [Tooltip("脚底向下的有效探测深度（米）")]
    public float castDistance = 0.35f;

    [Tooltip("有 CharacterController 时：投射半径 = 胶囊半径 × 此值（需 <1 避免蹭墙误判）")]
    public float castRadiusFactor = 0.85f;

    [Tooltip("用射线精修命中法线（球扫在斜面边缘的法线会被混合）")]
    public bool refineNormal = true;

    [Header("无 CharacterController 时的回退参数（Rigidbody 等方案）")]
    [Tooltip("投射半径（米）")]
    public float fallbackRadius = 0.35f;

    [Tooltip("物体轴心到脚底的高度（米），轴心在脚底则填 0")]
    public float fallbackOriginHeight = 0f;

    [Header("防抖")]
    [Tooltip("状态翻转的角度滞回量（度），避免在阈值附近反复触发事件")]
    public float angleHysteresis = 5f;

    [Tooltip("状态翻转需连续确认的帧数")]
    public int confirmFrames = 2;

    [Header("地面类型映射（预留扩展）")]
    [Tooltip("按 Layer 映射地面类型 id（如 1=普通 2=冰面 3=泥地），消费方按 id 决定行为；未命中为 0")]
    public List<GroundTypeEntry> groundTypes = new List<GroundTypeEntry>();

    [System.Serializable]
    public struct GroundTypeEntry
    {
        public LayerMask mask;
        public int surfaceId;
    }

    private void OnValidate()
    {
        maxSlopeAngle = Mathf.Clamp(maxSlopeAngle, 1f, 89f);
        minSlopeAngle = Mathf.Clamp(minSlopeAngle, 0f, maxSlopeAngle - 1f);
        angleHysteresis = Mathf.Max(0f, angleHysteresis);
        confirmFrames = Mathf.Max(1, confirmFrames);
        castDistance = Mathf.Max(0.05f, castDistance);
        castRadiusFactor = Mathf.Clamp(castRadiusFactor, 0.1f, 0.99f);
        fallbackRadius = Mathf.Max(0.05f, fallbackRadius);
        fallbackOriginHeight = Mathf.Max(0f, fallbackOriginHeight);
    }
}
