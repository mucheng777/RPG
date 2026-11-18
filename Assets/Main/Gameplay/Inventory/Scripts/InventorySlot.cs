using System;
using UnityEngine;

/// <summary>
/// 背包单个槽位的运行时数据（纯数据类，不引用任何 UI / 场景对象）。
///
/// 设计要点：
///   - Item == null 即空槽，不额外维护 bool 状态，避免两处状态不一致；
///   - 数量下限由 Inventory 统一保证，槽位自身只提供 IsEmpty / 剩余空间查询。
/// </summary>
[Serializable]
public class InventorySlot
{
    [Tooltip("物品定义；为空表示空槽")]
    public ItemDefinition Item;

    [Min(0)]
    [Tooltip("当前堆叠数量")]
    public int Count;

    public bool IsEmpty => Item == null || Count <= 0;

    /// <summary>该槽还能再放多少个同类物品（空槽返回 0，避免误当成容量）</summary>
    public int RemainingSpace => IsEmpty ? 0 : Mathf.Max(0, Item.maxStack - Count);

    public void Clear()
    {
        Item = null;
        Count = 0;
    }

    public void Set(ItemDefinition item, int count)
    {
        Item = item;
        Count = item == null ? 0 : Mathf.Clamp(count, 0, item.maxStack);
        if (Count <= 0) Clear();
    }
}
