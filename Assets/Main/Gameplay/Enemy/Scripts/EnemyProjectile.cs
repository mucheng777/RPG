using UnityEngine;

/// <summary>
/// 敌人炸弹：抛物线飞行，撞到玩家扣血、撞到地面/墙消失，超时也消失（回池）。
///
/// 不需要 Rigidbody / Collider，运动与命中全由本脚本驱动 ——
/// 与 EnemyBrain 的手动移动同一思路：物理引擎不参与，行为完全可控。
/// 预制体上只要一个视觉模型（球体 mesh）+ 本脚本即可，Collider 记得删掉。
///
/// 为什么用 SphereCast 而不是"飞过去再 OverlapSphere"：
///   每帧位移 = 速度 × deltaTime，速度快时一帧能飞半米以上，
///   帧末离散检测会直接从玩家身上"跳过去"（隧穿）。
///   SphereCast 扫过本帧位移的整段路径，撞到什么立刻结算，不会漏。
///
/// 为什么分 obstacleMask / damageMask：
///   obstacleMask —— 撞到什么就消失（玩家、地面、墙、其他怪都算）；
///   damageMask   —— 只有这些层才扣血（只勾 Player）。
///   所以"撞地面消失但不掉血、撞玩家消失且掉血"是同一段代码的两个分支。
/// </summary>
[AddComponentMenu("RPG/Enemy/Enemy Projectile")]
public class EnemyProjectile : MonoBehaviour, IPoolable
{
    [Header("命中")]
    [Tooltip("撞到就消失的层：勾 Player + Ground + Default（地面/墙/玩家都要）")]
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Tooltip("只有这些层会扣血：只勾 Player")]
    [SerializeField] private LayerMask damageMask = 1 << 6;

    [Tooltip("命中判定半径（球多大就填多少）")]
    [SerializeField] private float hitRadius = 0.25f;

    [Header("飞行")]
    [Tooltip("重力加速度（负数向下）。0 = 直线飞行；-12 左右 = 有弧线的抛物线")]
    [SerializeField] private float gravity = -12f;

    [Header("生命周期")]
    [Tooltip("最大存活时间（秒），到期自动回池，防止打不中的弹永远飞")]
    [SerializeField] private float lifeTime = 5f;

    private Vector3 _velocity;      // 当前速度向量（含垂直分量）
    private float _damage;
    private Transform _source;      // 发射者，用于忽略自身碰撞
    private float _lifeTimer;

    private static readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

    /// <summary>由 EnemyRangedAttack 在生成后立刻调用，注入飞行参数</summary>
    public void Launch(Vector3 direction, float speed, float damage, Transform source)
    {
        _velocity = direction * speed;
        _damage = damage;
        _source = source;
        _lifeTimer = 0f;
    }

    void Update()
    {
        if (_velocity.sqrMagnitude < 0.0001f) return; // 还没发射（或已被回收），什么都不做

        // 抛物线：每帧给垂直速度加重力，轨迹自然下坠并最终砸到地面
        _velocity.y += gravity * Time.deltaTime;

        Vector3 stepVec = _velocity * Time.deltaTime;
        float step = stepVec.magnitude;
        if (step < 0.0001f) return;

        // 连续检测：扫过本帧位移的整段路径
        if (TryHit(stepVec / step, step, out RaycastHit hit))
        {
            // 只有命中层在 damageMask 里才扣血；撞地面/墙时这里不会执行
            if (((1 << hit.collider.gameObject.layer) & damageMask) != 0)
            {
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                damageable?.TakeDamage(new DamageInfo(_damage, _source, DamageType.Physical));
            }
            DespawnSelf();
            return;
        }

        transform.position += stepVec;

        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= lifeTime) DespawnSelf();
    }

    /// <summary>扫本帧路径，返回最近的有效命中（排除发射者自己和弹体自身）</summary>
    private bool TryHit(Vector3 dir, float step, out RaycastHit nearest)
    {
        nearest = default;
        int count = Physics.SphereCastNonAlloc(
            transform.position, hitRadius, dir, _hitBuffer, step,
            obstacleMask, QueryTriggerInteraction.Ignore);

        float minDist = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = _hitBuffer[i];
            Transform root = h.collider.transform.root;
            if (root == transform.root) continue;                         // 弹体自己
            if (_source != null && root == _source.root) continue;        // 发射者

            if (h.distance < minDist)
            {
                minDist = h.distance;
                nearest = h;
                found = true;
            }
        }
        return found;
    }

    private void DespawnSelf()
    {
        if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
        else Destroy(gameObject);
    }

    // ==================== IPoolable（由 PoolManager 调用） ====================

    /// <summary>池取出时清零：Launch 之前保持静止，防止复用后按上次的参数继续乱飞</summary>
    public void OnSpawned()
    {
        _velocity = Vector3.zero;
        _lifeTimer = 0f;
        _damage = 0f;
        _source = null;
    }

    public void OnDespawned() { }
}
