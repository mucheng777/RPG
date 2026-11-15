using UnityEngine;

/// <summary>
/// 近战攻击组件：订阅 EnemyBrain 的出手事件，对目标直接结算伤害。
///
/// 与 EnemyRangedAttack 平级可互换：
///   Brain 只负责"什么时候出手"（AttackExecuted 事件），
///   本组件负责"出手时干什么"——贴脸直接扣血。
///   换远程怪时卸下本组件、改挂 EnemyRangedAttack 即可，Brain 零改动。
///
/// 伤害协议与玩家攻击一致：只认 IDamageable 接口，不感知 PlayerHealth 具体类。
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
[AddComponentMenu("RPG/Enemy/Enemy Melee Attack")]
public class EnemyMeleeAttack : MonoBehaviour
{
    [Tooltip("开发期打印攻击结算日志，打包前关闭")]
    [SerializeField] private bool debugLog = true;

    private EnemyBrain _brain;

    void Awake()
    {
        _brain = GetComponent<EnemyBrain>();
    }

    void OnEnable()
    {
        _brain.AttackExecuted += HandleAttack;
    }

    void OnDisable()
    {
        _brain.AttackExecuted -= HandleAttack;
    }

    /// <summary>Brain 出手时回调。距离校验已由状态机保证，这里不再做范围判定</summary>
    private void HandleAttack()
    {
        Transform target = _brain.Target;
        EnemyProfile profile = _brain.Profile;
        if (target == null || profile == null) return;

        var damageable = target.GetComponentInParent<IDamageable>();
        if (debugLog)
        {
            // 诊断：打印出手前后目标真实血量，区分"没扣血"与"扣了但血条没刷新"
            float before = damageable is IHealthSystem hs ? hs.CurrentHealth : -1f;
            Debug.Log($"[MeleeAttack] {name} 近战出手 {target.name}，伤害 {profile.attackDamage}，" +
                      $"目标组件 {(damageable != null ? damageable.GetType().Name : "找不到 IDamageable！")}，" +
                      $"目标当前血量 {before}", this);
        }
        damageable?.TakeDamage(new DamageInfo(profile.attackDamage, transform, DamageType.Physical));
        if (debugLog && damageable is IHealthSystem after)
            Debug.Log($"[MeleeAttack] 结算后目标血量 {after.CurrentHealth}", this);
    }
}
