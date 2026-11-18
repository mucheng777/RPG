using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单条掉落配置：物品 + 数量区间 + 权重。
/// </summary>
[Serializable]
public class LootEntry
{
    [Tooltip("掉落的物品定义")]
    public ItemDefinition item;

    [Min(1)]
    [Tooltip("单次掉落最小数量")]
    public int minCount = 1;

    [Min(1)]
    [Tooltip("单次掉落最大数量")]
    public int maxCount = 1;

    [Min(0f)]
    [Tooltip("权重（0 = 永不掉落）。每条被抽中的概率 = 自身权重 / 所有有效权重之和")]
    public float weight = 1f;
}

/// <summary>一次掉落结算结果（物品 + 数量），不依赖任何场景对象。</summary>
public struct LootDrop
{
    public ItemDefinition item;
    public int count;

    public LootDrop(ItemDefinition item, int count)
    {
        this.item = item;
        this.count = count;
    }
}

/// <summary>
/// 掉落表（ScriptableObject）—— 纯数据 + 纯随机，不引用场景、不自己生成物体。
///
/// 职责边界：
///   - 本类只回答"这次死亡掉什么、掉几个"（Roll）；
///   - 真正生成地面实体由 LootSpawner 负责（贴地、散落、对象池）；
///   - 敌人死亡触发由 EnemyLootDropper 负责（订阅 IHealthSystem.OnDied）。
/// 因此换掉落规则不需要动生成逻辑，换生成方式也不需要动掉落规则。
///
/// 扩展点：
///   - 保底掉落 / 概率加成（幸运值）：给 Roll 增加参数或子类化；
///   - 分级掉落表：新增 LootTable 资产并在 EnemyLootDropper 中按条件切换。
/// </summary>
[CreateAssetMenu(fileName = "LootTable", menuName = "RPG/Inventory/Loot Table")]
public class LootTable : ScriptableObject
{
    [Header("掉落次数")]
    [Min(0)]
    [Tooltip("每次死亡最少抽几次")]
    public int minRolls = 1;

    [Min(0)]
    [Tooltip("每次死亡最多抽几次")]
    public int maxRolls = 2;

    [Header("地面散落")]
    [Min(0f)]
    [Tooltip("以死亡点为中心的随机散落半径（米）")]
    public float scatterRadius = 1.2f;

    [Tooltip("贴地检测层：建议只勾选地面层，避免掉到墙壁/怪物身上")]
    public LayerMask groundMask = ~0;

    [Tooltip("掉落物预制体（需挂 ItemPickup）。留空则运行时生成占位实体")]
    public GameObject dropPrefab;

    [Header("掉落内容")]
    public List<LootEntry> entries = new List<LootEntry>();

    /// <summary>
    /// 结算一次掉落：先随机次数，再按权重抽条目，最后随机数量。
    /// 返回结果只包含有效物品，可能为空列表（表未配置时）。
    /// </summary>
    public List<LootDrop> Roll()
    {
        var result = new List<LootDrop>();

        int min = Mathf.Max(0, minRolls);
        int max = Mathf.Max(min, maxRolls);
        int rolls = UnityEngine.Random.Range(min, max + 1);

        for (int i = 0; i < rolls; i++)
        {
            LootEntry entry = PickWeighted();
            if (entry == null || entry.item == null) continue;

            int countMin = Mathf.Max(1, Mathf.Min(entry.minCount, entry.maxCount));
            int countMax = Mathf.Max(countMin, Mathf.Max(entry.minCount, entry.maxCount));
            int count = UnityEngine.Random.Range(countMin, countMax + 1);

            result.Add(new LootDrop(entry.item, count));
        }

        return result;
    }

    /// <summary>按权重随机取一条；权重全为 0 或列表为空时返回 null。</summary>
    private LootEntry PickWeighted()
    {
        float total = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            LootEntry e = entries[i];
            if (e != null && e.item != null && e.weight > 0f) total += e.weight;
        }
        if (total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < entries.Count; i++)
        {
            LootEntry e = entries[i];
            if (e == null || e.item == null || e.weight <= 0f) continue;
            roll -= e.weight;
            if (roll <= 0f) return e;
        }
        return null;
    }
}
