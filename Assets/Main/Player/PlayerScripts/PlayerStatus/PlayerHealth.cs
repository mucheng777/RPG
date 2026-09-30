using UnityEngine;

/// <summary>
/// 玩家专属血量组件，继承 HealthComponent。
/// 
/// 扩展点（非侵入式，不影响 HealthComponent 核心逻辑）：
///   1. 受伤无敌帧：TakeDamage 前置判断 _invincibleTimer，被击中后进入无敌期
///   2. 以后可加护盾、格挡减伤等，只需 override TakeDamage
///
/// 注意：不修改 PlayerController / PlayerCombat，血量是独立模块。
/// </summary>
public class PlayerHealth : HealthComponent
{
    [Header("玩家专属")]
    [Tooltip("受伤后无敌时间（秒）")]
    [SerializeField] private float invincibleDuration = 0.5f;

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
}
