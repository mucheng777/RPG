using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 拾取提示 UI —— 玩家靠近地面掉落物时，屏幕下方显示"最近的那件物品"的信息。
///
/// 交互：
///   - 只做信息展示，不解锁光标、不接管鼠标，视角和操作全程不受影响；
///   - 按 E 拾取当前显示的物品。
///
/// 解耦说明：
///   - 只认识 ItemPickup（数据 + 拾取入口）和 Inventory（数据），不关心物品类型；
///   - 通过 ItemPickup.ActivePickups 做距离筛选，不依赖物理触发器，避免每帧 OverlapSphere；
///   - 背包打开、暂停、死亡、主菜单时自动隐藏，不残留提示。
/// </summary>
[DefaultExecutionOrder(60)]
[AddComponentMenu("RPG/Inventory/Pickup Prompt UI")]
public class PickupPromptUI : MonoBehaviour
{
    [Header("字体")]
    [Tooltip("UI 字体：推荐 Assets/Fonts/SourceHanSansSC-Regular.otf")]
    [SerializeField] private Font uiFont;

    [Header("拾取")]
    [Tooltip("进入这个距离内显示最近掉落物的信息")]
    [Min(0.5f)]
    [SerializeField] private float pickupRange = 3f;

    [Tooltip("快捷拾取按键")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E;

    [Header("配色")]
    [SerializeField] private Color panelColor = new Color(0.10f, 0.12f, 0.15f, 0.94f);
    [SerializeField] private Color textColor = new Color(0.94f, 0.96f, 0.98f, 1f);

    private GameObject _canvasRoot;
    private RectTransform _panel;
    private Text _label;

    private Inventory _inventory;
    private InventoryUI _inventoryUI;
    private ItemPickup _target;
    private int _lastCount = -1;
    private bool _visible;

    private Font CurrentFont => uiFont != null ? uiFont : InventoryUIFactory.DefaultFont;

    void Awake()
    {
        BuildUI();
    }

    void Start()
    {
        _inventory = FindFirstObjectByType<Inventory>();
        _inventoryUI = InventoryUI.Instance;
    }

    void OnDisable()
    {
        SetVisible(false);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && !gm.IsPlaying) { SetVisible(false); return; }

        if (_inventoryUI == null) _inventoryUI = InventoryUI.Instance;
        if (_inventoryUI != null && _inventoryUI.IsOpen) { SetVisible(false); return; }

        if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();

        // 目标被外部销毁/回收（存在时间到期、被其他途径拾取）时，Unity 的 == null 为 true，
        // 这里必须显式清掉，否则信息条会一直停留在屏幕上
        if (_target == null && _visible)
        {
            _lastCount = -1;
            SetVisible(false);
        }

        ItemPickup nearest = FindNearest();
        if (nearest != _target)
        {
            _target = nearest;
            SetVisible(_target != null);
            if (_target != null) UpdateLabel();
        }
        else if (_target != null && _target.Count != _lastCount)
        {
            UpdateLabel();
        }

        if (_target != null && Input.GetKeyDown(pickupKey)) Collect();
    }

    // ==================== 查找 / 显示 ====================

    private ItemPickup FindNearest()
    {
        Vector3 origin = _inventory != null ? _inventory.transform.position : transform.position;
        float bestSqr = pickupRange * pickupRange;
        ItemPickup best = null;

        // 倒序遍历：允许遍历过程中安全移除已销毁对象
        var pickups = ItemPickup.ActivePickups;
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            ItemPickup pickup = pickups[i];
            if (pickup == null || pickup.Item == null) continue;

            float sqr = (pickup.transform.position - origin).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = pickup;
        }

        return best;
    }

    private void UpdateLabel()
    {
        if (_label == null || _target == null) return;

        _lastCount = _target.Count;
        _label.text = $"[{pickupKey}] 拾取 {_target.Item.displayName} ×{_target.Count} · {_target.Item.itemType.GetDisplayName()}";
    }

    private void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;

        // 只切换显示，不碰光标/相机输入：靠近战利品时依然保持锁定视角
        if (_panel != null) _panel.gameObject.SetActive(visible);
    }

    private void Collect()
    {
        if (_target == null) return;
        if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
        if (_inventory == null) return;

        if (_target.TryCollect(_inventory))
        {
            _target = null;
            _lastCount = -1;
            SetVisible(false);
        }
        else if (_label != null)
        {
            _label.text = "背包已满";
        }
    }

    // ==================== 构建界面 ====================

    private void BuildUI()
    {
        var canvasGo = new GameObject("PickupCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        _canvasRoot = canvasGo;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // 低于背包界面

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // 纯展示条：不挂 Button、不接收射线，避免鼠标被它拦截
        Image panelImage = InventoryUIFactory.CreateImage("Prompt", canvasGo.transform, panelColor,
                                                          InventoryUIFactory.RoundedSprite, true);
        panelImage.raycastTarget = false;

        _panel = panelImage.rectTransform;
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0f);
        _panel.pivot = new Vector2(0.5f, 0f);
        _panel.anchoredPosition = new Vector2(0f, 120f);
        _panel.sizeDelta = new Vector2(420f, 72f);

        _label = InventoryUIFactory.CreateText("Label", _panel, CurrentFont, "拾取", 20,
                                               textColor, TextAnchor.MiddleCenter);
        InventoryUIFactory.Stretch(_label.rectTransform, 16f, 16f, 8f, 8f);

        _canvasRoot.SetActive(true);
        _panel.gameObject.SetActive(false);
        _visible = false;
    }
}
