using UnityEngine;

/// <summary>
/// 近战攻击组件：Brain 出手时只记录目标，真正结算交给攻击动画上的 AttackBegin 事件。
///
/// 为什么不在 AttackExecuted 里直接扣血：
///   AttackExecuted 只是给 Animator 设 Trigger，动画下一帧才开始播。
///   在事件里立刻 TakeDamage，等于伤害发生在动画第 0 帧，玩家看到的是"还没咬就被打"。
///
/// 现在改为：动画播到咬合帧（Chomper_Attack.anim 的 0.572 秒）触发 AttackBegin，
/// 此时才判定伤害，视觉与判定严格对齐。
///
/// 注意：换新攻击动画时，必须在剪辑里补一个同名的 AttackBegin 事件，否则不会咬人。
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
[AddComponentMenu("RPG/Enemy/Enemy Melee Attack")]
public class EnemyMeleeAttack : MonoBehaviour
{
    [Tooltip("开发期打印攻击结算日志，打包前关闭")]
    [SerializeField] private bool debugLog = true;

    private EnemyBrain _brain;
    private Transform _pendingTarget;

    void Awake()
    {
        _brain = GetComponent<EnemyBrain>();
    }

    void OnEnable()
    {
        _brain.AttackExecuted += HandleAttackStart;
    }

    void OnDisable()
    {
        _brain.AttackExecuted -= HandleAttackStart;
        _pendingTarget = null;
    }

    /// <summary>出手瞬间：只记录目标，不结算。动画播到咬合帧才真正判定。</summary>
    private void HandleAttackStart()
    {
        _pendingTarget = _brain.Target;
    }

    /// <summary>动画事件（Chomper_Attack.anim，0.572 秒咬合瞬间）：此时才结算伤害。</summary>
    public void AttackBegin()
    {
        Transform target = _pendingTarget;
        _pendingTarget = null;
        if (target == null) return;

        EnemyProfile profile = _brain.Profile;
        if (profile == null) return;

        // 前摇期间目标跑出攻击范围 → 挥空（避免"明明躲开了还是被咬"）
        Vector3 diff = target.position - transform.position;
        diff.y = 0f;
        if (diff.sqrMagnitude > profile.attackRange * profile.attackRange * 1.69f) return;

        var damageable = target.GetComponentInParent<IDamageable>();
        if (debugLog)
        {
            // 诊断：打印出手前后目标真实血量，区分"没扣血"与"扣了但血条没刷新"
            float before = damageable is IHealthSystem hs ? hs.CurrentHealth : -1f;
            Debug.Log($"[MeleeAttack] {name} 咬中 {target.name}，伤害 {profile.attackDamage}，" +
                      $"目标组件 {(damageable != null ? damageable.GetType().Name : "找不到 IDamageable！")}，" +
                      $"目标当前血量 {before}", this);
        }

        // 只通过接口交互：攻击方不知道、也不需要知道对方血量怎么扣
        damageable?.TakeDamage(new DamageInfo(profile.attackDamage, transform, DamageType.Physical));

        if (debugLog && damageable is IHealthSystem after)
            Debug.Log($"[MeleeAttack] 结算后目标血量 {after.CurrentHealth}", this);
    }
}
