using UnityEngine;

/// <summary>
/// 玩家专属血量组件，继承 HealthComponent，实现 IRevivable（供游戏流程系统复活）。
/// 
/// 扩展点（非侵入式，不影响 HealthComponent 核心逻辑）：
///   1. 受伤无敌帧：TakeDamage 前置判断 _invincibleTimer，被击中后进入无敌期
///   2. 以后可加护盾、格挡减伤等，只需 override TakeDamage
///   3. 复活：实现 IRevivable.Revive，并重写 HandleDeath 让流程系统接管死亡处理
///
/// 注意：不修改 PlayerController / PlayerCombat，血量是独立模块。
/// </summary>
public class PlayerHealth : HealthComponent, IRevivable
{
    [Header("玩家专属")]
    [Tooltip("受伤后无敌时间（秒）")]
    [SerializeField] private float invincibleDuration = 0.5f;

    [Header("复活")]
    [Tooltip("复活后的无敌时间（秒），避免刚复活就被贴脸秒杀")]
    [SerializeField] private float reviveInvincibility = 1.5f;

    private float _invincibleTimer;

    void Update()
    {
        if (_invincibleTimer > 0f)
            _invincibleTimer -= Time.deltaTime;
    }

    public override void TakeDamage(DamageInfo info)
    {
        // 无敌帧期间忽略伤害
        if (_invincibleTimer > 0f) return;

        base.TakeDamage(info);

        // 存活才进入无敌期（死后不需要）
        if (IsAlive)
            _invincibleTimer = invincibleDuration;
    }

    // ==================== IRevivable ====================

    /// <summary>
    /// 复活：满血 + 解除死亡锁 + 广播血条刷新 + 给予复活无敌。
    /// 只复位"数据状态"；位置复位由 PlayerLifecycleBridge 负责（职责分离）。
    /// 幂等：对存活对象重复调用无副作用。
    /// </summary>
    public void Revive()
    {
        _currentHealth = MaxHealth;
        _isAlive = true;
        _invincibleTimer = reviveInvincibility;
        NotifyHealthChanged();
    }

    // ==================== 死亡接管 ====================

    /// <summary>
    /// 玩家死亡不销毁对象：改由 GameFlow 生命周期系统接管，
    /// 保留对象以便原地复活（与父类"按 HealthProfile.destroyDelay 延迟销毁"的默认行为不同）。
    /// </summary>
    protected override void HandleDeath() { }
}
