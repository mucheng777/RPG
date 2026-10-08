using UnityEngine;

/// <summary>
/// 攻击 → 生命值系统桥接组件（非侵入式）。
///
/// 独立挂在 Player 根物体即可工作，不修改 PlayerCombat / PlayerController：
/// 自行读取 Animator 中 Attack1/Attack2 的播放进度，在命中窗口内做一次
/// OverlapSphere，对命中物的 IDamageable 调用 TakeDamage(DamageInfo)。
///
/// 依赖方向（依赖倒置原则）：
///   本组件 → IDamageable（抽象）      不依赖 EnemyHealth / PlayerHealth（具体）
/// 所以新增任何敌人类型，只要挂 HealthComponent 就能被砍，本组件零修改。
///
/// 单次命中保证：
///   每段攻击用 _hitDone 锁，只在窗口第一次进入时判定一次；
///   状态切换/离开攻击状态自动复位，避免一次挥砍每帧扣血。
///
/// 更精确的升级方案：
///   把命中判定改为动画剪辑上的 Animation Event，事件函数直接调 PerformHit(stage)，
///   外部管线不变（接口隔离带来的可替换性）。
/// </summary>
[AddComponentMenu("RPG/Attack Hit Detection")]
public class AttackHitDetection : MonoBehaviour
{
    [Header("命中盒")]
    [Tooltip("命中球相对角色中心的偏移（建议向前 + 腰高）")]
    [SerializeField] private Vector3 hitOffset = new Vector3(0f, 1f, 0.9f);

    [Tooltip("命中球半径")]
    [SerializeField] private float hitRadius = 1.3f;

    [Tooltip("可命中层（只勾 Enemy，勿勾 Player/Default）")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("命中窗口（动画 normalizedTime）")]
    [SerializeField] private float hitWindowStart = 0.35f;
    [SerializeField] private float hitWindowEnd = 0.65f;

    [Tooltip("Animator 攻击层索引")]
    [SerializeField] private int attackLayerIndex = 1;

    [Header("伤害")]
    [Tooltip("第一段伤害")]
    [Min(0f)]
    [SerializeField] private float stage1Damage = 20f;

    [Tooltip("第二段伤害（重击更高）")]
    [Min(0f)]
    [SerializeField] private float stage2Damage = 30f;

    private Animator _animator;
    private readonly Collider[] _hitBuffer = new Collider[16];
    private bool _hitDone;
    private int _lastStage;

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (_animator == null) return;

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(attackLayerIndex);

        int stage = 0;
        if (state.IsName("Attack1")) stage = 1;
        else if (state.IsName("Attack2")) stage = 2;

        // 进入新段或离开攻击状态 → 复位单次命中锁
        if (stage == 0 || stage != _lastStage)
        {
            _lastStage = stage;
            _hitDone = false;
        }

        if (stage == 0 || _hitDone) return;

        float nt = state.normalizedTime % 1f;
        if (nt < hitWindowStart || nt > hitWindowEnd) return;

        PerformHit(stage);
        _hitDone = true;
    }

    /// <summary>
    /// 执行一次范围命中。public 以便 Animation Event 直接调用（帧精确方案）。
    /// </summary>
    public void PerformHit(int stage)
    {
        float damage = stage == 2 ? stage2Damage : stage1Damage;
        if (!DamageCalculator.IsValidDamage(damage)) return;

        Vector3 center = transform.TransformPoint(hitOffset);
        int count = Physics.OverlapSphereNonAlloc(center, hitRadius, _hitBuffer, hitMask);

        for (int i = 0; i < count; i++)
        {
            // 模型是多层子物体：沿碰撞体向上找受伤接口
            var target = _hitBuffer[i].GetComponentInParent<IDamageable>();
            if (target == null) continue;

            // 防止误伤自己（玩家自己也实现了 IDamageable）
            if (target is Component component && component.transform.root == transform.root) continue;

            // 只通过接口交互：攻击方不知道、也不需要知道对方血量怎么扣
            target.TakeDamage(new DamageInfo(damage, transform, DamageType.Physical));
        }
    }

    // Scene 视图选中时画出命中球，方便调偏移/半径
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.TransformPoint(hitOffset), hitRadius);
    }
}
