using UnityEngine;

/// <summary>
/// 全局拖拽上下文 —— 记录"当前正被拖动的物品来自哪里、是什么、几个"。
///
/// 为什么单独抽出来：
///   - 槽位之间拖拽、外部容器（宝箱/商店/装备栏）拖入背包，共用同一份上下文；
///   - 新增拖拽来源（比如任务奖励列表）时，只需调用 BeginExternal，不用改槽位代码。
///
/// 约定：拖拽全程不提前修改背包数据，只在"放下成功"时一次性提交，
/// 这样中途取消拖拽不会丢物品。
/// </summary>
public static class ItemDragContext
{
    /// <summary>一次拖拽的载荷</summary>
    public class Payload
    {
        public ItemDefinition Item;
        public int Count;

        /// <summary>来源背包（外部拖入时为 null）</summary>
        public Inventory Source;

        /// <summary>来源槽位下标（外部拖入时为 -1）</summary>
        public int SourceIndex;

        public bool IsExternal => Source == null || SourceIndex < 0;
    }

    public static Payload Current { get; private set; }

    public static bool IsDragging => Current != null;

    /// <summary>从背包槽位开始拖拽</summary>
    public static void Begin(ItemDefinition item, int count, Inventory source, int sourceIndex)
    {
        if (item == null || count <= 0) { Clear(); return; }
        Current = new Payload { Item = item, Count = count, Source = source, SourceIndex = sourceIndex };
    }

    /// <summary>
    /// 从外部来源开始拖拽（后续宝箱、装备栏、商店可直接调用）。
    /// 外部来源没有"原位"，放下失败时物品回到原容器由调用方自己负责。
    /// </summary>
    public static void BeginExternal(ItemDefinition item, int count)
    {
        if (item == null || count <= 0) { Clear(); return; }
        Current = new Payload { Item = item, Count = count, Source = null, SourceIndex = -1 };
    }

    public static void Clear()
    {
        Current = null;
    }
}
