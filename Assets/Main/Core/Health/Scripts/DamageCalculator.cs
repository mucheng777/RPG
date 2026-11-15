using UnityEngine;

/// <summary>
/// 伤害计算器（纯静态、无状态）。
/// 纯函数设计：相同输入必定得到相同输出 → 不依赖场景即可写 EditMode 单元测试。
///
/// 当前规则：
///   护甲边际递减减伤：mitigation = armor / (armor + K)
///   特点：护甲收益永远达不到 100%，前期收益高、后期递减，防止堆甲无敌。
///   最小伤害保底 1 点：保证"打中了一定有反馈"。
///
/// 扩展示例（不改调用方）：
///   - 抗性表：按 DamageType 查表加乘区
///   - 暴击：传入攻击者 CombatStats 后在本类掷骰
///   - 易伤/减伤 buff：新增 multiplier 参数
/// </summary>
public static class DamageCalculator
{
    /// <summary>护甲公式常数 K：K 越大护甲越"软"。100 = 100 点护甲提供 50% 减伤。</summary>
    private const float ArmorConstantK = 100f;

    /// <summary>单次承伤最小扣血（保底反馈）</summary>
    private const float MinDamage = 1f;

    /// <summary>当前护甲对应的减伤率（0~1），供 UI/调试显示</summary>
    public static float GetMitigation(float armor)
    {
        armor = Mathf.Max(0f, armor);
        return armor / (armor + ArmorConstantK);
    }

    /// <summary>
    /// 计算最终扣血量。
    /// </summary>
    /// <param name="rawDamage">原始伤害（必须为有效正数）</param>
    /// <param name="armor">受击方护甲（&lt;0 按 0 处理）</param>
    /// <param name="bypassArmor">是否无视护甲（真实伤害 / 穿甲效果）</param>
    public static float Calculate(float rawDamage, float armor, bool bypassArmor = false)
    {
        if (!IsValidDamage(rawDamage)) return 0f;

        if (bypassArmor)
            return rawDamage;

        float finalDamage = rawDamage * (1f - GetMitigation(armor));
        return Mathf.Max(MinDamage, finalDamage);
    }

    /// <summary>
    /// 伤害数值有效性校验（边界条件统一入口）。
    /// NaN / Infinity / 负数 / 0 都视为非法：0 伤害无战斗意义，负数会变成"治疗"。
    /// </summary>
    public static bool IsValidDamage(float amount)
    {
        return !float.IsNaN(amount)
               && !float.IsInfinity(amount)
               && amount > 0f;
    }
}
