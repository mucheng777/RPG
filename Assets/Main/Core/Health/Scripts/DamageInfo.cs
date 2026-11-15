using UnityEngine;

/// <summary>
/// 伤害信息包。
/// 攻击方创建实例，传给 IDamageable.TakeDamage。
/// 字段设计为只读，防止受击方篡改。
/// </summary>
public struct DamageInfo
{
    /// <summary>伤害数值</summary>
    public readonly float Amount;

    /// <summary>伤害来源 Transform（用于击退方向、仇恨目标等）</summary>
    public readonly Transform Source;

    /// <summary>伤害类型（以后扩展：物理/魔法/真实伤害）</summary>
    public readonly DamageType Type;

    public DamageInfo(float amount, Transform source, DamageType type = DamageType.Physical)
    {
        Amount = amount;
        Source = source;
        Type = type;
    }
}

/// <summary>
/// 伤害类型枚举。
/// 当前只有物理伤害，预留给以后扩展抗性系统。
/// </summary>
public enum DamageType
{
    Physical,
    Magical,
    True
}
