using System;

/// <summary>
/// 可受伤契约。
/// 攻击方（PlayerCombat 等）只依赖此接口，不依赖具体目标类型 →
/// 玩家砍敌人、敌人砍玩家、敌人砍敌人，全部走同一套伤害管线。
/// </summary>
public interface IDamageable
{
    /// <summary>受击事件（参数：伤害信息，用于播放受击动画/音效/击退）</summary>
    event Action<DamageInfo> OnDamaged;

    /// <summary>承受伤害</summary>
    void TakeDamage(DamageInfo info);
}
