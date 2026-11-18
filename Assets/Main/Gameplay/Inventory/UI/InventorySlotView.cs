using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 背包槽位视图 —— 只负责"显示一个槽位 + 把指针事件翻译成背包操作请求"。
///
/// 交互约定（全部交给 InventoryUI 决策，槽位不直接改数据）：
///   - 左键拖拽：交换 / 合并（Shift + 拖拽 = 只拖一半）
///   - 右键：拆分一半到第一个空槽
///   - 拖到其他槽位：放下时统一由 InventoryUI 提交数据变更
/// </summary>
[AddComponentMenu("RPG/Inventory/Inventory Slot View")]
public class InventorySlotView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    private InventoryUI _owner;
    private int _index;

    private Image _background;
    private Image _border;
    private Image _icon;
    private Text _countText;

    private InventorySlot _slot;
    private bool _hover;
    private bool _dragging;

    /// <summary>由 InventoryUI 在创建槽位时注入视觉引用（保持本类不关心构建细节）</summary>
    public void Setup(InventoryUI owner, int index, Image background, Image border, Image icon, Text countText)
    {
        _owner = owner;
        _index = index;
        _background = background;
        _border = border;
        _icon = icon;
        _countText = countText;
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

        if (_icon != null)
        {
            _icon.enabled = !empty;
            if (!empty)
            {
                _icon.sprite = _slot.Item.icon != null ? _slot.Item.icon : InventoryUIFactory.WhiteSprite;
                _icon.color = _slot.Item.icon != null ? Color.white : _slot.Item.iconColor;
            }
        }

        if (_countText != null)
            _countText.text = !empty && _slot.Count > 1 ? _slot.Count.ToString() : string.Empty;

        if (_border != null)
            _border.color = empty ? _owner.EmptyBorderColor : _slot.Item.rarityColor;

        if (_background != null)
            _background.color = _hover && !empty ? _owner.HoverSlotColor : _owner.NormalSlotColor;
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            _owner?.RequestSplit(_index);
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
