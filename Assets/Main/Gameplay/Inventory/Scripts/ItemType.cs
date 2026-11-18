/// <summary>
/// 物品分类：背包排序、整理、掉落表筛选的第一维度。
///
/// 扩展方式：只在此处追加枚举值（枚举值之间刻意留出间隔，便于插入新分类），
/// 背包、掉落、UI 都按 ItemType 泛化处理，新增类型不需要改动任何现有逻辑。
/// </summary>
public enum ItemType
{
    /// <summary>材料（矿石、皮革等）</summary>
    Material = 0,

    /// <summary>消耗品（药水等，使用逻辑后续接入）</summary>
    Consumable = 10,

    /// <summary>装备（武器/护甲，后续接入装备栏）</summary>
    Equipment = 20,

    /// <summary>任务物品</summary>
    Quest = 30
}

public static class ItemTypeExtensions
{
    /// <summary>中文显示名（UI 用，逻辑层不参与比较）</summary>
    public static string GetDisplayName(this ItemType type)
    {
        switch (type)
        {
            case ItemType.Material: return "材料";
            case ItemType.Consumable: return "消耗品";
            case ItemType.Equipment: return "装备";
            case ItemType.Quest: return "任务物品";
            default: return type.ToString();
        }
    }
}
