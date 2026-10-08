using UnityEngine;

/// <summary>
/// 敌人配置（ScriptableObject）。
/// 符合项目惯例：配置与逻辑分离。
///
/// 组合复用（不重复定义血量）：
///   血量/死亡/UI 相关字段全部归属 HealthProfile，本资产只持有它的引用。
///   - "小怪A/小怪B"可共用同一份 HealthProfile（都是 50 血），
///     各自的 EnemyProfile 只配不同的攻击/移速/感知；
///   - 改血量只有 HealthProfile 一个真相源，不会出现两边数值不一致；
///   - HealthProfile 同时可被玩家、Boss、可破坏物复用。
///
/// 便捷属性（maxHealth / destroyDelay / showHealthBar）只读转发，
/// 让消费方少写一层 healthProfile.xxx，且 healthProfile 未指定时有兜底值。
/// </summary>
[CreateAssetMenu(fileName = "EnemyProfile", menuName = "RPG/Enemy Profile")]
public class EnemyProfile : ScriptableObject
{
    [Header("血量配置")]
    [Tooltip("拖入一份 HealthProfile；不填时便捷属性使用兜底值（50 血 / 2 秒销毁）")]
    public HealthProfile healthProfile;

    [Header("移动")]
    [Tooltip("巡逻/追击移动速度")]
    public float moveSpeed = 2f;

    [Header("攻击")]
    [Tooltip("攻击伤害")]
    public float attackDamage = 10f;

    [Tooltip("攻击间隔（秒）")]
    public float attackInterval = 2f;

    [Tooltip("攻击距离")]
    public float attackRange = 1.5f;

    [Header("AI 感知（以后扩展用）")]
    [Tooltip("追击触发距离")]
    public float chaseRange = 8f;

    [Tooltip("丢失目标距离")]
    public float loseTargetRange = 12f;

    // ==================== HealthProfile 便捷转发（只读） ====================

    /// <summary>最大血量（转发自 HealthProfile，未指定时兜底 50）</summary>
    public float maxHealth => healthProfile != null ? healthProfile.maxHealth : 50f;

    /// <summary>死亡后延迟销毁时间（转发自 HealthProfile，未指定时兜底 2 秒）</summary>
    public float destroyDelay => healthProfile != null ? healthProfile.destroyDelay : 2f;

    /// <summary>是否显示血条（转发自 HealthProfile，未指定时默认显示）</summary>
    public bool showHealthBar => healthProfile == null || healthProfile.showHealthBar;
}
