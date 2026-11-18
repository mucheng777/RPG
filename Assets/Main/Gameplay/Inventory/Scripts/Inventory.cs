using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>背包整理方式（后续可按稀有度/等级扩展）。</summary>
public enum InventorySortMode
{
    /// <summary>按物品类型分组，同类按名称排序</summary>
    ByType,

    /// <summary>按堆叠总数量从多到少排序</summary>
    ByCount
}

/// <summary>
/// 背包数据组件（挂在玩家身上）—— 只负责"格子 + 物品 + 增删改查 + 排序"，零 UI 依赖。
///
/// 设计要点（低耦合 / 可扩展）：
///   1. 数据与表现分离：本类不引用任何 UI；UI 订阅 OnChanged 刷新，逻辑层不感知界面；
///   2. 容量可扩展：SetCapacity 支持扩容（缩容要求多余槽位为空，避免物品丢失）；
///   3. 操作全部带返回值：UI/拾取/交易都通过 bool / int 判断结果，不靠异常和隐式状态；
///   4. 跨容器转移：静态 Transfer 支持两个背包之间移动/合并/交换，
///      后续做宝箱、商店、装备栏时可直接复用，无需重写拖拽逻辑。
///
/// 扩展点：
///   - 存档：在文件底部按注释实现 CaptureState / RestoreState 即可，槽位结构已可序列化；
///   - 使用物品：由外部（InventoryUI / 快捷键）调用 RemoveAt 后再执行效果；
///   - 容量升级：调用 SetCapacity(Capacity + n)。
/// </summary>
[AddComponentMenu("RPG/Inventory/Inventory")]
public class Inventory : MonoBehaviour
{
    [Header("容量")]
    [Min(1)]
    [Tooltip("初始槽位数量（6 的倍数可让网格排满一行）")]
    [SerializeField] private int capacity = 24;

    [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

    /// <summary>背包内容或容量发生任何变化时广播（UI 唯一刷新入口）</summary>
    public event Action OnChanged;

    public int Capacity => slots.Count;
    public IReadOnlyList<InventorySlot> Slots => slots;

    void Awake()
    {
        EnsureCapacity(capacity);
    }

    // ==================== 容量 ====================

    /// <summary>只增不减地保证容量（初始化/扩容用，不会清空已有物品）</summary>
    public void EnsureCapacity(int target)
    {
        while (slots.Count < target) slots.Add(new InventorySlot());
    }

    /// <summary>
    /// 设置容量。扩容总是成功；缩容只在被裁掉的槽位全部为空时成功，
    /// 避免"改容量把物品改没了"这类数据丢失。
    /// </summary>
    public bool SetCapacity(int target)
    {
        target = Mathf.Max(1, target);
        if (target == slots.Count) return true;

        if (target > slots.Count)
        {
            EnsureCapacity(target);
            NotifyChanged();
            return true;
        }

        for (int i = target; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty) return false;
        }

        slots.RemoveRange(target, slots.Count - target);
        NotifyChanged();
        return true;
    }

    // ==================== 查询 ====================

    public InventorySlot GetSlot(int index)
        => index >= 0 && index < slots.Count ? slots[index] : null;

    /// <summary>某物品在整个背包里的总数</summary>
    public int CountItem(ItemDefinition item)
    {
        if (item == null) return 0;
        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && slots[i].Item == item) total += slots[i].Count;
        }
        return total;
    }

    /// <summary>某物品还能放多少个（已有堆的剩余空间 + 空槽整堆空间）</summary>
    public int GetFreeSpace(ItemDefinition item)
    {
        if (item == null) return 0;
        int space = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.IsEmpty) space += item.maxStack;
            else if (slot.Item == item) space += Mathf.Max(0, slot.Item.maxStack - slot.Count);
        }
        return space;
    }

    public bool CanAdd(ItemDefinition item, int count)
        => item != null && count > 0 && GetFreeSpace(item) >= count;

    // ==================== 增删 ====================

    /// <summary>
    /// 添加物品：先填已有同类堆，再占用空槽。返回"实际放进去了多少"。
    /// 放不下时只放一部分（拾取/掉落都依赖这个语义），不会丢数据。
    /// </summary>
    public int TryAdd(ItemDefinition item, int count)
    {
        if (item == null || count <= 0) return 0;

        int remaining = count;

        // 1. 优先堆叠到已有同类槽位
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.IsEmpty || slot.Item != item) continue;

            int add = Mathf.Min(remaining, slot.Item.maxStack - slot.Count);
            if (add <= 0) continue;

            slot.Count += add;
            remaining -= add;
        }

        // 2. 再占用空槽
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            InventorySlot slot = slots[i];
            if (!slot.IsEmpty) continue;

            int add = Mathf.Min(remaining, item.maxStack);
            slot.Item = item;
            slot.Count = add;
            remaining -= add;
        }

        int added = count - remaining;
        if (added > 0) NotifyChanged();
        return added;
    }

    /// <summary>从指定槽位移除指定数量；数量不足则整体失败（不做部分移除）。</summary>
    public bool RemoveAt(int index, int count)
    {
        InventorySlot slot = GetSlot(index);
        if (slot == null || slot.IsEmpty || count <= 0 || slot.Count < count) return false;

        slot.Count -= count;
        if (slot.Count <= 0) slot.Clear();

        NotifyChanged();
        return true;
    }

    /// <summary>
    /// 外部来源放入指定槽位（拖拽/外部容器用）。
    /// 返回实际放入数量：目标为空或同类时可放，否则返回 0。
    /// </summary>
    public int PlaceInto(int index, ItemDefinition item, int count)
    {
        InventorySlot slot = GetSlot(index);
        if (slot == null || item == null || count <= 0) return 0;

        if (slot.IsEmpty)
        {
            int add = Mathf.Min(count, item.maxStack);
            slot.Item = item;
            slot.Count = add;
            NotifyChanged();
            return add;
        }

        if (slot.Item != item) return 0;

        int space = slot.Item.maxStack - slot.Count;
        int moved = Mathf.Min(count, Mathf.Max(0, space));
        if (moved <= 0) return 0;

        slot.Count += moved;
        NotifyChanged();
        return moved;
    }

    /// <summary>
    /// 拆分：把该槽一半（向下取整）移到第一个空槽。
    /// 没有空槽或数量小于 2 时失败。
    /// </summary>
    public bool SplitStack(int index)
    {
        InventorySlot slot = GetSlot(index);
        if (slot == null || slot.IsEmpty || slot.Count < 2) return false;

        int emptyIndex = FindFirstEmptyIndex();
        if (emptyIndex < 0) return false;

        int half = slot.Count / 2;
        if (half <= 0) return false;

        slots[emptyIndex].Item = slot.Item;
        slots[emptyIndex].Count = half;
        slot.Count -= half;

        NotifyChanged();
        return true;
    }

    public int FindFirstEmptyIndex()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty) return i;
        }
        return -1;
    }

    // ==================== 转移（拖拽核心，支持跨容器） ====================

    /// <summary>
    /// 把 source 的 fromIndex 槽位中 count 个物品转移到 target 的 toIndex 槽位。
    /// 规则：
    ///   - 目标为空：直接放入（整堆 = 移动，部分 = 拆分）；
    ///   - 目标同类：合并到目标堆，目标放不下的部分留在原位；
    ///   - 目标不同类：只有"整堆转移"才交换（拖拽交换）；部分转移直接拒绝。
    /// 返回是否发生了移动。跨背包（宝箱/商店）同样适用。
    /// </summary>
    public static bool Transfer(Inventory source, int fromIndex, Inventory target, int toIndex, int count)
    {
        if (source == null || target == null) return false;

        InventorySlot from = source.GetSlot(fromIndex);
        InventorySlot to = target.GetSlot(toIndex);
        if (from == null || to == null || from.IsEmpty || count <= 0) return false;

        count = Mathf.Min(count, from.Count);
        bool wholeStack = count == from.Count;

        // 1. 目标为空：放入
        if (to.IsEmpty)
        {
            to.Item = from.Item;
            to.Count = count;

            from.Count -= count;
            if (from.Count <= 0) from.Clear();

            source.NotifyChanged();
            if (target != source) target.NotifyChanged();
            return true;
        }

        // 2. 目标同类：合并
        if (to.Item == from.Item)
        {
            int space = Mathf.Max(0, to.Item.maxStack - to.Count);
            int moved = Mathf.Min(count, space);
            if (moved <= 0) return false;

            to.Count += moved;
            from.Count -= moved;
            if (from.Count <= 0) from.Clear();

            source.NotifyChanged();
            if (target != source) target.NotifyChanged();
            return true;
        }

        // 3. 目标不同类：仅整堆交换
        if (!wholeStack) return false;

        ItemDefinition tempItem = to.Item;
        int tempCount = to.Count;

        to.Item = from.Item;
        to.Count = from.Count;
        from.Item = tempItem;
        from.Count = tempCount;

        source.NotifyChanged();
        if (target != source) target.NotifyChanged();
        return true;
    }

    // ==================== 整理 ====================

    /// <summary>
    /// 整理背包：先按规则聚合物品，再从第 0 格开始紧凑重排。
    /// 不破坏堆叠上限（超过单格上限会自动拆成多格）。
    /// </summary>
    public void Sort(InventorySortMode mode)
    {
        // 1. 聚合：同类物品合并总数，并保留首次出现顺序
        var totals = new List<KeyValuePair<ItemDefinition, int>>();
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.IsEmpty) continue;

            int found = -1;
            for (int j = 0; j < totals.Count; j++)
            {
                if (totals[j].Key == slot.Item) { found = j; break; }
            }

            if (found >= 0)
                totals[found] = new KeyValuePair<ItemDefinition, int>(totals[found].Key, totals[found].Value + slot.Count);
            else
                totals.Add(new KeyValuePair<ItemDefinition, int>(slot.Item, slot.Count));
        }

        // 2. 排序
        if (mode == InventorySortMode.ByType)
        {
            totals.Sort((a, b) =>
            {
                int typeCompare = a.Key.itemType.CompareTo(b.Key.itemType);
                if (typeCompare != 0) return typeCompare;
                return string.Compare(a.Key.displayName, b.Key.displayName, StringComparison.Ordinal);
            });
        }
        else
        {
            totals.Sort((a, b) => b.Value.CompareTo(a.Value));
        }

        // 3. 紧凑回填
        int cursor = 0;
        for (int i = 0; i < totals.Count; i++)
        {
            ItemDefinition item = totals[i].Key;
            int remaining = totals[i].Value;

            while (remaining > 0 && cursor < slots.Count)
            {
                int stack = Mathf.Min(remaining, item.maxStack);
                slots[cursor].Item = item;
                slots[cursor].Count = stack;
                remaining -= stack;
                cursor++;
            }
        }

        for (int i = cursor; i < slots.Count; i++) slots[i].Clear();

        NotifyChanged();
    }

    // ==================== 内部 ====================

    private void NotifyChanged()
    {
        OnChanged?.Invoke();
    }

    // ==================== 存档扩展点 ====================
    // 需要存档时按下面思路实现（槽位已可序列化，不需要改数据结构）：
    //
    // [Serializable] public class SaveData { public List<string> itemIds; public List<int> counts; public int capacity; }
    // public SaveData CaptureState() { ... 用 itemId 而非资产引用，保证跨版本稳定 ... }
    // public void RestoreState(SaveData data) { ... 通过 ItemDatabase 按 id 反查 ItemDefinition ... }
    //
    // 还需要一个 ItemDatabase（SO 里放 List<ItemDefinition>）承担 id → 资产的映射。
}
