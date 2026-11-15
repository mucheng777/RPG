//逻辑层，真正写数据的，实现接口的类
using UnityEngine;

/// <summary>
/// 斜面判定核心组件（挂在角色根物体上）。
/// 每帧向下检测并缓存斜面快照，对外仅通过 ISlopeDataProvider 只读接口供数，
/// 通过 [DefaultExecutionOrder(-10)] 保证先于角色移动逻辑（PlayerController，order 0）执行。
/// 内部实现（检测方式、防抖策略）完全封闭：
/// - 切换移动方案（CharacterController ↔ Rigidbody）只影响 ResolveProbe 的分支，接口不变；
/// - 换角色 / 换地形只换 SlopeProfile 配置，不改此文件。
/// </summary>
[DefaultExecutionOrder(-10)]//保证先于角色移动逻辑执行，确保斜面判定准确
[AddComponentMenu("RPG/Slope Detector")]
public class SlopeDetector : MonoBehaviour, ISlopeDataProvider//在这里识别了接口，就可以用了
{
    [Tooltip("斜面配置资产；为空时运行时使用默认参数")]
    [SerializeField] private SlopeProfile profile;

    private CharacterController _controller;
    private SlopeProfile _runtimeProfile;   // 未指定 profile 时的运行时兜底
    // 初始为"未接地、可通行"，与接口语义（未接地时 IsSlopeWalkable 恒为 true）一致
    private SlopeSnapshot _committed = new SlopeSnapshot(false, false, true, 0f, Vector3.up, Vector3.zero, 0);//_committed 是真正存数据的地方
    private SlopeSnapshot _candidate;       // 防抖中的候选状态
    private int _pendingFrames;

    // ==================== ISlopeDataProvider 只读数据 ====================
    public bool IsGrounded => _committed.IsGrounded;
    public bool IsOnSlope => _committed.IsOnSlope;
    public bool IsSlopeWalkable => _committed.IsWalkable;
    public float SlopeAngle => _committed.SlopeAngle;
    public Vector3 GroundNormal => _committed.GroundNormal;
    public Vector3 GroundPoint => _committed.GroundPoint;
    public int CurrentSurfaceId => _committed.SurfaceId;

    /// <summary>当前帧完整快照</summary>
    public SlopeSnapshot Snapshot => _committed;

    public event System.Action<SlopeSnapshot> OnSlopeStateChanged;
    // ====================================================================

    private CharacterController Controller
    {
        get
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            return _controller;
        }
    }

    private SlopeProfile ResolvedProfile
    {
        get
        {
            if (profile != null) return profile;

            if (Application.isPlaying)
            {
                if (_runtimeProfile == null)
                {
                    _runtimeProfile = ScriptableObject.CreateInstance<SlopeProfile>();
                    Debug.LogWarning("[SlopeDetector] 未指定 SlopeProfile，使用运行时默认参数。", this);
                }
                return _runtimeProfile;
            }
            return null; // 编辑模式下未配置则不工作
        }
    }

    private void Update()
    {
        SlopeProfile p = ResolvedProfile;
        if (p == null) return;

        SlopeSnapshot candidate = Evaluate(CastGround());

        if (SameState(candidate, _committed))
        {
            _pendingFrames = 0;
        }
        else
        {
            if (!SameState(candidate, _candidate))
            {
                _candidate = candidate;
                _pendingFrames = 1;
            }
            else
            {
                _pendingFrames++;
            }

            if (_pendingFrames >= Mathf.Max(1, p.confirmFrames))
            {
                _committed = candidate;
                _pendingFrames = 0;
                OnSlopeStateChanged?.Invoke(_committed);
            }
        }
    }

    // ==================== 检测（内部实现，完全封闭） ====================

    private readonly struct GroundHit
    {
        public readonly bool Hit;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Collider Collider;
        public readonly Vector3 Origin;      // Gizmos 绘制用
        public readonly float Radius;
        public readonly float Distance;

        private GroundHit(bool hit, Vector3 point, Vector3 normal, Collider collider,
            Vector3 origin, float radius, float distance)
        {
            Hit = hit;
            Point = point;
            Normal = normal;
            Collider = collider;
            Origin = origin;
            Radius = radius;
            Distance = distance;
        }

        public static GroundHit Miss(Vector3 origin, float radius, float distance)
            => new GroundHit(false, Vector3.zero, Vector3.up, null, origin, radius, distance);

        public static GroundHit Contact(Vector3 point, Vector3 normal, Collider collider,
            Vector3 origin, float radius, float distance)
            => new GroundHit(true, point, normal, collider, origin, radius, distance);
    }

    /// <summary>根据是否存在 CharacterController 解析检测参数并向下检测一次</summary>
    private GroundHit CastGround()
    {
        SlopeProfile p = ResolvedProfile;
        if (p == null)
            return GroundHit.Miss(transform.position, 0.1f, 0.1f);

        CharacterController cc = Controller;
        Vector3 origin;
        float radius;
        float distance;

        if (cc != null)
        {
            // 胶囊底面中心（胶囊轴始终与世界竖直对齐）
            Vector3 bottomCenter = transform.TransformPoint(
                new Vector3(cc.center.x, cc.center.y - cc.height * 0.5f, cc.center.z));
            radius = Mathf.Max(cc.radius * p.castRadiusFactor, 0.01f);
            // 球底与胶囊底对齐：起点不会陷入地面，且整体位于胶囊内，不会误检自身
            origin = bottomCenter + Vector3.up * radius;
            distance = radius + p.castDistance + cc.skinWidth;
        }
        else
        {
            radius = Mathf.Max(p.fallbackRadius, 0.01f);
            origin = transform.position + Vector3.up * (p.fallbackOriginHeight + radius);
            distance = radius + p.castDistance;
        }

        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit sphereHit,
            distance, p.groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 point = sphereHit.point;
            Vector3 normal = sphereHit.normal;

            if (p.refineNormal)
            {
                Vector3 refineOrigin = point + Vector3.up * 0.05f;
                if (Physics.Raycast(refineOrigin, Vector3.down, out RaycastHit rayHit, 0.25f,
                    p.groundMask, QueryTriggerInteraction.Ignore)
                    && rayHit.collider == sphereHit.collider)
                {
                    point = rayHit.point;
                    normal = rayHit.normal;
                }
            }

            return GroundHit.Contact(point, normal, sphereHit.collider, origin, radius, distance);
        }

        return GroundHit.Miss(origin, radius, distance);
    }

    /// <summary>由原始命中结果计算状态快照（滞回以已提交状态为基准）</summary>
    private SlopeSnapshot Evaluate(in GroundHit hit)
    {
        SlopeProfile p = ResolvedProfile;
        if (p == null || !hit.Hit)
            return new SlopeSnapshot(false, false, true, 0f, Vector3.up, Vector3.zero, 0);

        float angle = Vector3.Angle(Vector3.up, hit.Normal);
        float hyst = p.angleHysteresis;

        // 进入斜坡需越过阈值+滞回，离开需低于阈值-滞回，中间区域保持原状态
        bool onSlope = _committed.IsOnSlope
            ? angle >= p.minSlopeAngle - hyst
            : angle >= p.minSlopeAngle + hyst;

        bool walkable = angle <= (_committed.IsWalkable
            ? p.maxSlopeAngle + hyst
            : p.maxSlopeAngle - hyst);

        return new SlopeSnapshot(true, onSlope, walkable, angle, hit.Normal,
            hit.Point, ResolveSurfaceId(hit.Collider));
    }

    private int ResolveSurfaceId(Collider groundCollider)
    {
        SlopeProfile p = ResolvedProfile;
        if (p == null || groundCollider == null || p.groundTypes == null || p.groundTypes.Count == 0)
            return 0;

        int layerBit = 1 << groundCollider.gameObject.layer;
        for (int i = 0; i < p.groundTypes.Count; i++)
        {
            if ((p.groundTypes[i].mask.value & layerBit) != 0)
                return p.groundTypes[i].surfaceId;
        }
        return 0;
    }

    private static bool SameState(in SlopeSnapshot a, in SlopeSnapshot b)
        => a.IsGrounded == b.IsGrounded
           && a.IsOnSlope == b.IsOnSlope
           && a.IsWalkable == b.IsWalkable
           && a.SurfaceId == b.SurfaceId;

    // ==================== 调试 ====================

    [ContextMenu("Run Detection Now")]
    private void RunDetectionNow()
    {
        GroundHit hit = CastGround();
        Debug.Log($"[SlopeDetector] {Evaluate(hit)}\nHit:{hit.Hit} Point:{hit.Point} Normal:{hit.Normal}", this);
    }

    private void OnDrawGizmosSelected()
    {
        SlopeProfile p = ResolvedProfile;
        if (p == null) return;

        GroundHit hit = CastGround();

        // 投射路径：起点球 → 终点球
        Gizmos.color = hit.Hit ? Color.white : Color.gray;
        Gizmos.DrawWireSphere(hit.Origin, hit.Radius);
        Vector3 end = hit.Origin + Vector3.down * hit.Distance;
        Gizmos.DrawLine(hit.Origin, end);
        Gizmos.DrawWireSphere(end, hit.Radius);

        if (!hit.Hit) return;

        // 命中点十字
        Gizmos.color = Color.white;
        const float cross = 0.08f;
        Gizmos.DrawLine(hit.Point + Vector3.left * cross, hit.Point + Vector3.right * cross);
        Gizmos.DrawLine(hit.Point + Vector3.forward * cross, hit.Point + Vector3.back * cross);

        // 法线箭头：绿=平地 / 黄=可走斜坡 / 红=超限
        float angle = Vector3.Angle(Vector3.up, hit.Normal);
        Gizmos.color = angle < p.minSlopeAngle ? Color.green
            : angle <= p.maxSlopeAngle ? Color.yellow : Color.red;
        Vector3 tip = hit.Point + hit.Normal * 0.6f;
        Gizmos.DrawLine(hit.Point, tip);
        Gizmos.DrawLine(tip, tip + (Vector3.down - hit.Normal * 0.3f).normalized * 0.08f);

        // 下坡方向
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, hit.Normal);
        if (downhill.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(hit.Point, hit.Point + downhill.normalized * 0.5f);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(hit.Point + Vector3.up * 0.35f, $"Slope {angle:F1}");
#endif
    }
}
