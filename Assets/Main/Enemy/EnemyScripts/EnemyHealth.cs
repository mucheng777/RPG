using UnityEngine;

/// <summary>
/// 敌人血量组件，继承 HealthComponent。
///
/// 扩展点（为以后 AI 预留）：
///   1. OnDamaged 订阅可触发受击硬直（让 EnemyBrain 切到 HurtState）
///   2. OnDied 触发死亡动画 + 通知 EnemyBrain 切到 DeadState
///   3. 以后可加弱点倍率（爆头 2x 伤害）→ override TakeDamage
/// </summary>
public class EnemyHealth : HealthComponent
{
    [Header("敌人专属")]
    [Tooltip("受击是否播放硬直动画（需要 EnemyAnimator 配合）")]
    [SerializeField] private bool playHurtAnimation = true;

    protected override void HandleDeath()
    {
        // TODO: 播放死亡动画，动画结束后再销毁
        // 当前阶段：直接走父类逻辑（延迟销毁）
        base.HandleDeath();
    }
}
