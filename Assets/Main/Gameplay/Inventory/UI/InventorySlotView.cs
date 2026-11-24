using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 背包槽位视图 —— 只负责"显示一个槽位 + 把指针事件翻译成背包操作请求"。
///
/// 交互约定（全部交给 InventoryUI 决策，槽位不直接改数据）：
///   - 左键拖拽：移动到空槽 / 与目标同类合并 / 与目标不同类交换
///   - 拖到其他槽位：放下时统一由 InventoryUI 提交数据变更
/// </summary>
[AddComponentMenu("RPG/Inventory/Inventory Slot View")]
public class InventorySlotView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("视觉引用（在槽位预制体上指定）")]
    [Tooltip("槽位底框：负责显示稀有度颜色，必须开启 Raycast Target 才能接收拖拽")]
    [SerializeField] private Image borderImage;

    [Tooltip("槽位底板：悬停时变色（不需要就留空）")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("物品图标")]
    [SerializeField] private Image iconImage;

    [Tooltip("数量文字（不需要就留空）")]
    [SerializeField] private TMP_Text countTextLabel;

    [Header("配色（空的格子 / 悬停时）")]
    [Tooltip("空格子的边框颜色")]
    [SerializeField] private Color emptyBorderColor = new Color(1f, 1f, 1f, 0.10f);

    [Tooltip("普通状态的底板颜色（你的美术图想保持原色就填纯白）")]
    [SerializeField] private Color normalBackgroundColor = new Color(0.15f, 0.16f, 0.20f, 1f);

    [Tooltip("鼠标悬停时的底板颜色")]
    [SerializeField] private Color hoverBackgroundColor = new Color(0.23f, 0.25f, 0.31f, 1f);

    private InventoryUI _owner;
    private int _index;

    private InventorySlot _slot;
    private bool _hover;
    private bool _dragging;
    private Sprite _iconDefaultSprite;

    /// <summary>由 InventoryUI 创建槽位时调用：注入归属与索引，视觉引用早就在预制体上配好了</summary>
    public void Bind(InventoryUI owner, int index)
    {
        _owner = owner;
        _index = index;

        // 记住预制体自带的占位图：物品没有图标时回退到它
        if (iconImage != null) _iconDefaultSprite = iconImage.sprite;
    }

    public void Refresh(InventorySlot slot)
    {
        _slot = slot;
        ApplyVisual();
    }

    public void SetHover(bool hover)
    {
        _hover = hover;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (_owner == null) return;

        bool empty = _slot == null || _slot.IsEmpty;

        if (iconImage != null)
        {
            iconImage.enabled = !empty;
            if (!empty)
            {
                iconImage.sprite = _slot.Item.icon != null ? _slot.Item.icon : _iconDefaultSprite;
                iconImage.color = _slot.Item.icon != null ? Color.white : _slot.Item.iconColor;
            }
        }

        if (countTextLabel != null)
            countTextLabel.text = !empty && _slot.Count > 1 ? _slot.Count.ToString() : string.Empty;

        if (borderImage != null)
            borderImage.color = empty ? emptyBorderColor : _slot.Item.rarityColor;

        if (backgroundImage != null)
            backgroundImage.color = _hover && !empty ? hoverBackgroundColor : normalBackgroundColor;
    }

    // ==================== 指针事件 ====================

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHover(true);
        _owner?.OnSlotHover(_index, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHover(false);
        _owner?.OnSlotHover(_index, false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _owner == null) return;

        _owner.BeginDragFromSlot(_index);
        _dragging = ItemDragContext.IsDragging;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragging) _owner?.MoveDragGhost(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;
        _owner?.EndDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        _owner?.HandleDrop(_index);
    }
}
