using System;
using UnityEngine;

/// <summary>
/// 血量数据契约。
/// 任何需要"有血量"的角色（玩家、敌人、NPC）都实现此接口。
/// UI（HealthBarUI）只依赖此接口，不依赖具体类型 → 玩家/敌人复用同一套UI。
/// </summary>
public interface IHealthProvider
{
    /// <summary>最大血量</summary>
    float MaxHealth { get; }

    /// <summary>当前血量</summary>
    float CurrentHealth { get; }

    /// <summary>是否存活</summary>
    bool IsAlive { get; }

    /// <summary>血量变化事件（参数：当前血量, 最大血量）</summary>
    event Action<float, float> OnHealthChanged;

    /// <summary>死亡事件</summary>
    event Action OnDied;

    /// <summary>治疗</summary>
    void Heal(float amount);
}
