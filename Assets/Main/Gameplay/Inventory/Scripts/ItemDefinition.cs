using UnityEngine;

/// <summary>
/// 物品定义（ScriptableObject）—— 物品数据的唯一真相源。
///
/// 与项目既有 EnemyProfile / HealthProfile 同一套惯例：配置与逻辑分离。
/// 新增物品只需要 Assets → Create → RPG/Inventory/Item Definition 新建一个资产，零代码。
///
/// 美术接入点（当前全部可用纯色占位，不需要任何素材）：
///   - icon        ：背包图标，留空 = 用 iconColor 纯色方块
///   - worldPrefab ：地面掉落物预制体（需挂 ItemPickup），留空 = 程序生成纯色球体
///
/// 扩展点：
///   - 使用效果：新增 IItemUseEffect 接口 + 在 Inventory/装备栏调用即可，本类不用改；
///   - 装备属性：新增 ItemEquipmentData 子资产或在本类追加可选引用。
/// </summary>
[CreateAssetMenu(fileName = "Item_New", menuName = "RPG/Inventory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("标识")]
    [Tooltip("唯一 ID（存档、跨系统引用用）。留空时自动使用资产名")]
    public string itemId;

    [Tooltip("显示名称")]
    public string displayName;

    [TextArea(2, 4)]
    [Tooltip("描述（背包详情面板展示）")]
    public string description;

    [Header("分类与堆叠")]
    [Tooltip("物品分类：影响排序归类与后续筛选")]
    public ItemType itemType = ItemType.Material;

    [Min(1)]
    [Tooltip("单格最大堆叠数量")]
    public int maxStack = 99;

    [Header("视觉（留空 = 纯色占位）")]
    [Tooltip("稀有度颜色：只影响背包槽位边框，后续可扩展为掉率/品质体系")]
    public Color rarityColor = new Color(0.45f, 0.50f, 0.58f, 1f);

    [Tooltip("背包图标：留空时用 iconColor 纯色方块占位")]
    public Sprite icon;

    [Tooltip("占位图标颜色")]
    public Color iconColor = Color.white;

    [Tooltip("地面掉落物的占位颜色（纯色球体）")]
    public Color worldColor = new Color(0.85f, 0.75f, 0.35f, 1f);

    [Tooltip("地面掉落物预制体（需挂 ItemPickup）。留空则程序生成占位球体")]
    public GameObject worldPrefab;

    void OnValidate()
    {
        // 兜底：避免策划漏填导致 UI 空白 / 分割错误
        if (string.IsNullOrEmpty(itemId)) itemId = name;
        if (string.IsNullOrEmpty(displayName)) displayName = name;
        maxStack = Mathf.Max(1, maxStack);
    }
}
