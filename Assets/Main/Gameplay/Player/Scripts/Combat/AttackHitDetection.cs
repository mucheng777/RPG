using System.Collections;
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

    [Header("伤害（每段可单独调，后面的段可以更高做重击感）")]
    [Min(0f)] [SerializeField] private float stage1Damage = 20f;
    [Min(0f)] [SerializeField] private float stage2Damage = 30f;
    [Min(0f)] [SerializeField] private float stage3Damage = 40f;
    [Min(0f)] [SerializeField] private float stage4Damage = 55f;

    [Header("打击感（命中反馈）")]
    [Tooltip("命中卡顿时长（秒，真实时间）")]
    [SerializeField] private float hitStopDuration = 0.06f;

    [Tooltip("卡顿期间的时间缩放（越小越顿）")]
    [SerializeField] private float hitStopScale = 0.05f;

    [Tooltip("相机震动时长（秒）")]
    [SerializeField] private float shakeDuration = 0.15f;

    [Tooltip("相机震动幅度（米）")]
    [SerializeField] private float shakeMagnitude = 0.12f;

    private Animator _animator;
    private readonly Collider[] _hitBuffer = new Collider[16];
    private bool _hitDone;
    private int _lastStage;
    private Coroutine _hitStopRoutine;

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (_animator == null) return;

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(attackLayerIndex);

        int stage = 0;
        if      (state.IsName("Attack1")) stage = 1;
        else if (state.IsName("Attack2")) stage = 2;
        else if (state.IsName("Attack3")) stage = 3;
        else if (state.IsName("Attack4")) stage = 4;

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
        float damage = stage switch
        {
            1 => stage1Damage,
            2 => stage2Damage,
            3 => stage3Damage,
            4 => stage4Damage,
            _ => stage1Damage
        };
        if (!DamageCalculator.IsValidDamage(damage)) return;

        Vector3 center = transform.TransformPoint(hitOffset);
        int count = Physics.OverlapSphereNonAlloc(center, hitRadius, _hitBuffer, hitMask);

        bool hitAny = false;
        for (int i = 0; i < count; i++)
        {
            // 模型是多层子物体：沿碰撞体向上找受伤接口
            var target = _hitBuffer[i].GetComponentInParent<IDamageable>();
            if (target == null) continue;

            // 防止误伤自己（玩家自己也实现了 IDamageable）
            if (target is Component component && component.transform.root == transform.root) continue;

            // 已死亡的尸体不再受击，也不触发打击反馈（否则砍尸体一样会顿帧）
            if (target is IHealthSystem health && !health.IsAlive) continue;

            // 只通过接口交互：攻击方不知道、也不需要知道对方血量怎么扣
            target.TakeDamage(new DamageInfo(damage, transform, DamageType.Physical));
            hitAny = true;
        }

        // 至少砍中一个活目标 → 顿帧 + 震屏
        if (hitAny)
        {
            HitStop();
            if (ThirdPersonCamera.Instance != null)
                ThirdPersonCamera.Instance.Shake(shakeDuration, shakeMagnitude);
        }
    }

    /// <summary>命中顿帧：短暂降低 timeScale，制造"砍中了"的重量感</summary>
    private void HitStop()
    {
        // 已有卡顿在进行中，或游戏本身处于暂停（timeScale ≈ 0）时不重复触发
        if (_hitStopRoutine != null || Time.timeScale < 0.5f) return;
        _hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        Time.timeScale = hitStopScale;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 1f;
        _hitStopRoutine = null;
    }

    // 组件禁用/退出 Play 时必须还原时间，否则 timeScale 会永久卡在 0.05 变成慢动作
    void OnDisable()
    {
        if (_hitStopRoutine == null) return;
        StopCoroutine(_hitStopRoutine);
        _hitStopRoutine = null;
        Time.timeScale = 1f;
    }

    // Scene 视图选中时画出命中球，方便调偏移/半径
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.TransformPoint(hitOffset), hitRadius);
    }
}
