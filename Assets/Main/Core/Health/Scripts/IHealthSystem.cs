using System;

/// <summary>
/// 生命值系统数据契约（ISP：只暴露"读状态 + 订阅事件 + 治疗"）。
/// 任何有血量的角色（玩家 / 敌人 / NPC / 可破坏物）实现此接口。
///
/// 设计意图：
///   - UI（HealthBarUI）、治疗技能、任务系统等"读血量"的一方只依赖本接口，
///     永远拿不到 TakeDamage，无法绕过伤害管线直接改血（接口隔离原则）。
///   - "造成伤害"的能力放在另一个契约 IDamageable，二者可独立实现。
///
/// 获取方式：GetComponent&lt;IHealthSystem&gt;()（与 HealthComponent 同一 GameObject）。
/// </summary>
public interface IHealthSystem
{
    /// <summary>最大血量（只读）</summary>
    float MaxHealth { get; }

    /// <summary>当前血量（只读，范围 0 ~ MaxHealth）</summary>
    float CurrentHealth { get; }

    /// <summary>是否存活</summary>
    bool IsAlive { get; }

    /// <summary>
    /// 血量变化事件（观察者模式）。参数：(当前血量, 最大血量)。
    /// UI 订阅此事件刷新，禁止每帧轮询 CurrentHealth。
    /// </summary>
    event Action<float, float> OnHealthChanged;

    /// <summary>死亡事件（仅触发一次，死亡锁保证）</summary>
    event Action OnDied;

    /// <summary>治疗（超出上限自动截断；死亡状态无效）</summary>
    void Heal(float amount);
}
