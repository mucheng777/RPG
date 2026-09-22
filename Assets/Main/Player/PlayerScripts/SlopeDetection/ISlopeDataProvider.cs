//接口/抽象层
using UnityEngine;

/// <summary>
/// 斜面判定数据契约：任何需要坡度信息的系统（移动、动画、音频等）
/// 通过该接口只读消费数据，与检测实现完全解耦。
/// 获取方式：GetComponent&lt;ISlopeDataProvider&gt;()（与 SlopeDetector 同一物体）。
/// </summary>
public interface ISlopeDataProvider
{
    /// <summary>是否接地（基于向下的物理检测；与 CharacterController.isGrounded 语义可能短暂不一致，本模块是独立数据源）</summary>
    bool IsGrounded { get; }

    /// <summary>是否站在斜坡上（接地且坡角达到斜坡阈值，含滞回防抖）</summary>
    bool IsOnSlope { get; }

    /// <summary>当前坡角是否在可攀爬范围内（含滞回防抖；未接地时恒为 true）</summary>
    bool IsSlopeWalkable { get; }

    /// <summary>坡面与水平面的夹角（度）。未接地时为 0。</summary>
    float SlopeAngle { get; }

    /// <summary>地面法线。未接地时为 Vector3.up。</summary>
    Vector3 GroundNormal { get; }

    /// <summary>地面命中点。未接地时为 Vector3.zero。</summary>
    Vector3 GroundPoint { get; }

    /// <summary>当前地面类型 id（由 SlopeProfile 的 Layer 映射表决定），未命中映射时为 0</summary>
    int CurrentSurfaceId { get; }

    /// <summary>斜面状态变化时触发（接地/离地、进入/离开斜坡、可走性翻转、地面类型变化）</summary>
    event System.Action<SlopeSnapshot> OnSlopeStateChanged;
}

/// <summary>某一帧的完整斜面状态快照（只读）</summary>
public readonly struct SlopeSnapshot
{
    public readonly bool IsGrounded;
    public readonly bool IsOnSlope;
    public readonly bool IsWalkable;
    public readonly float SlopeAngle;
    public readonly Vector3 GroundNormal;
    public readonly Vector3 GroundPoint;
    public readonly int SurfaceId;

    public SlopeSnapshot(bool isGrounded, bool isOnSlope, bool isWalkable, float slopeAngle,
        Vector3 groundNormal, Vector3 groundPoint, int surfaceId)
    {
        IsGrounded = isGrounded;
        IsOnSlope = isOnSlope;
        IsWalkable = isWalkable;
        SlopeAngle = slopeAngle;
        GroundNormal = groundNormal;
        GroundPoint = groundPoint;
        SurfaceId = surfaceId;
    }

    public override string ToString()
        => $"Grounded:{IsGrounded} OnSlope:{IsOnSlope} Walkable:{IsWalkable} Angle:{SlopeAngle:F1} Surface:{SurfaceId}";
}
