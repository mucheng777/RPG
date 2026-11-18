using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包界面（表现层）—— 只做"展示 + 交互翻译"，所有数据变更都通过 Inventory 的公开 API 提交。
///
/// 结构（运行时构建，零素材即可用）：
///   遮罩 → 主面板 → 标题/容量/关闭 → 6 列网格（ScrollRect + GridLayoutGroup）
///   → 右侧详情 → 底部整理按钮与操作提示 → 拖拽跟随图标
///
/// 交互：
///   - Tab / I：打开、关闭背包
///   - 左键拖拽：槽位间交换 / 合并；Shift + 拖拽 = 只拖一半
///   - 右键：拆分一半到第一个空槽
///   - 拖入外部物品：调用 BeginExternalDrag(...)（宝箱/商店/装备栏的接入点）
///
/// 与既有系统的耦合控制：
///   - 不修改 GameManager / ThirdPersonCamera / PlayerController 的代码；
///   - 打开时通过 InteractionCursorGate 临时解锁光标并暂停相机输入，
///     通过 Time.timeScale 冻结世界，通过禁用 PlayerController 防止点击 UI 触发攻击；
///   - 关闭或流程状态变化（暂停/死亡）时自动恢复，不残留状态。
/// </summary>
[DefaultExecutionOrder(50)]
[AddComponentMenu("RPG/Inventory/Inventory UI")]
public class InventoryUI : MonoBehaviour
{
    private static InventoryUI _instance;

    /// <summary>场景内背包 UI 单例（拾取提示需要查询"背包是否打开"）</summary>
    public static InventoryUI Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<InventoryUI>();
            return _instance;
        }
    }

    [Header("字体与按键")]
    [Tooltip("UI 字体：推荐 Assets/Fonts/SourceHanSansSC-Regular.otf（留空则使用内置字体，中文可能缺字）")]
    [SerializeField] private Font uiFont;

    [Tooltip("开关背包按键")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Tooltip("备用开关按键")]
    [SerializeField] private KeyCode alternateToggleKey = KeyCode.I;

    [Header("行为")]
    [Tooltip("打开背包时冻结游戏时间")]
    [SerializeField] private bool pauseWhileOpen = true;

    [Tooltip("打开背包时禁用玩家控制（避免点击 UI 同时挥砍）")]
    [SerializeField] private bool blockPlayerInputWhileOpen = true;

    [Header("配色（占位美术，后续可整体替换）")]
    [SerializeField] private Color panelColor = new Color(0.06f, 0.07f, 0.09f, 0.96f);
    [SerializeField] private Color normalSlotColor = new Color(0.15f, 0.16f, 0.20f, 1f);
    [SerializeField] private Color hoverSlotColor = new Color(0.23f, 0.25f, 0.31f, 1f);
    [SerializeField] private Color emptyBorderColor = new Color(1f, 1f, 1f, 0.10f);
    [SerializeField] private Color accentColor = new Color(0.36f, 0.78f, 0.55f, 1f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.94f, 0.96f, 1f);
    [SerializeField] private Color mutedTextColor = new Color(0.62f, 0.66f, 0.72f, 1f);

    // 槽位视图从 UI 拿配色，避免颜色散落在两个脚本里
    public Color NormalSlotColor => normalSlotColor;
    public Color HoverSlotColor => hoverSlotColor;
    public Color EmptyBorderColor => emptyBorderColor;

    public bool IsOpen => _isOpen;

    private readonly List<InventorySlotView> _views = new List<InventorySlotView>();

    private Inventory _inventory;
    private PlayerController _playerController;

    private Canvas _canvas;
    private GameObject _canvasRoot;
    private RectTransform _gridContent;
    private Text _capacityText;
    private Image _detailIcon;
    private Text _detailName;
    private Text _detailType;
    private Text _detailDesc;
    private RectTransform _dragGhost;
    private Image _dragGhostIcon;
    private Text _dragGhostCount;

    private bool _isOpen;
    private int _detailIndex = -1;
    private float _prevTimeScale = 1f;
    private bool _playerInputBlocked;

    private Font CurrentFont => uiFont != null ? uiFont : InventoryUIFactory.DefaultFont;

    // ==================== 生命周期 ====================

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        BuildUI();
    }

    void Start()
    {
        _inventory = FindFirstObjectByType<Inventory>();
        if (_inventory == null)
        {
            Debug.LogError("[InventoryUI] 场景中找不到 Inventory（背包）组件，请先运行 Tools/RPG/背包系统/一键搭建。", this);
            return;
        }

        _inventory.OnChanged += Refresh;
        Refresh();
    }

    void OnEnable()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.OnStateChanged -= HandleStateChanged;

        // 组件被禁用时不要让世界停留在冻结/光标解锁状态
        if (_isOpen) CloseInternal(false);
    }

    void OnDestroy()
    {
        if (_inventory != null) _inventory.OnChanged -= Refresh;
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) { Toggle(); return; }
        if (alternateToggleKey != KeyCode.None && Input.GetKeyDown(alternateToggleKey)) Toggle();
    }

    // ==================== 开关 ====================

    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (_isOpen) return;

        var gm = GameManager.Instance;
        if (gm != null && !gm.IsPlaying) return; // 只在游戏运行中打开，菜单/暂停/死亡不响应

        if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
        if (_inventory == null)
        {
            Debug.LogError("[InventoryUI] 找不到 Inventory 组件，无法打开背包。", this);
            return;
        }

        _isOpen = true;
        if (_canvasRoot != null) _canvasRoot.SetActive(true);

        _prevTimeScale = Time.timeScale;
        if (pauseWhileOpen) Time.timeScale = 0f;

        if (blockPlayerInputWhileOpen)
        {
            if (_playerController == null) _playerController = FindFirstObjectByType<PlayerController>();

            // 只记录"是我们关掉的"，关闭时也只恢复这一种，避免覆盖死亡/其他系统的禁用状态
            if (_playerController != null && _playerController.enabled)
            {
                _playerController.enabled = false;
                _playerInputBlocked = true;
            }
        }

        InteractionCursorGate.Acquire(this);
        Refresh();
    }

    public void Close()
    {
        CloseInternal(true);
    }

    private void CloseInternal(bool restoreTimeScale)
    {
        if (!_isOpen) return;

        _isOpen = false;
        EndDrag();
        ClearDetail();

        if (_canvasRoot != null) _canvasRoot.SetActive(false);

        if (_playerInputBlocked && _playerController != null)
        {
            _playerController.enabled = true;
            _playerInputBlocked = false;
        }

        InteractionCursorGate.Release(this);

        if (restoreTimeScale && pauseWhileOpen)
        {
            // 若期间已经切到暂停/死亡状态，时间由 GameManager 管，这里不能再改成 1
            var gm = GameManager.Instance;
            if (gm == null || gm.IsPlaying) Time.timeScale = _prevTimeScale;
        }
    }

    private void HandleStateChanged(GameState previous, GameState current)
    {
        if (_isOpen) CloseInternal(false);
    }

    // ==================== 刷新 ====================

    /// <summary>背包数据变化后的统一刷新入口（订阅 Inventory.OnChanged）</summary>
    public void Refresh()
    {
        if (_inventory == null) return;

        if (_views.Count != _inventory.Capacity) RebuildSlotViews();

        int used = 0;
        for (int i = 0; i < _views.Count; i++)
        {
            InventorySlot slot = _inventory.Slots[i];
            if (slot != null && !slot.IsEmpty) used++;
            if (_views[i] != null) _views[i].Refresh(slot);
        }

        if (_capacityText != null) _capacityText.text = $"{used} / {_inventory.Capacity}";

        if (_detailIndex >= 0) ShowDetail(_detailIndex);
    }

    private void RebuildSlotViews()
    {
        if (_gridContent == null) return;

        // 先隐藏再销毁，避免 Destroy 延迟一帧造成新旧槽位叠加显示
        for (int i = _gridContent.childCount - 1; i >= 0; i--)
        {
            GameObject child = _gridContent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        _views.Clear();

        for (int i = 0; i < _inventory.Capacity; i++)
            _views.Add(CreateSlotView(i));
    }

    private InventorySlotView CreateSlotView(int index)
    {
        Image border = InventoryUIFactory.CreateImage($"Slot_{index:00}", _gridContent, emptyBorderColor,
                                                      InventoryUIFactory.RoundedSprite, true);
        border.raycastTarget = true; // 接收指针事件（拖拽/悬停/右键）

        Image background = InventoryUIFactory.CreateImage("Bg", border.transform, normalSlotColor,
                                                         InventoryUIFactory.RoundedSprite, true);
        InventoryUIFactory.Stretch(background.rectTransform, 2f, 2f, 2f, 2f);

        Image icon = InventoryUIFactory.CreateImage("Icon", border.transform, Color.white,
                                                    InventoryUIFactory.WhiteSprite);
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 4f);
        iconRect.sizeDelta = new Vector2(58f, 58f);

        Text count = InventoryUIFactory.CreateText("Count", border.transform, CurrentFont, string.Empty,
                                                   16, textColor, TextAnchor.LowerRight);
        InventoryUIFactory.Stretch(count.rectTransform, 8f, 8f, 4f, 4f);

        var view = border.gameObject.AddComponent<InventorySlotView>();
        view.Setup(this, index, background, border, icon, count);
        return view;
    }

    // ==================== 详情面板 ====================

    public void OnSlotHover(int index, bool hover)
    {
        if (hover) ShowDetail(index);
    }

    private void ShowDetail(int index)
    {
        if (_inventory == null) return;

        InventorySlot slot = _inventory.GetSlot(index);
        _detailIndex = index;

        if (slot == null || slot.IsEmpty) { ClearDetail(); return; }

        if (_detailIcon != null)
        {
            _detailIcon.enabled = true;
            _detailIcon.sprite = slot.Item.icon != null ? slot.Item.icon : InventoryUIFactory.WhiteSprite;
            _detailIcon.color = slot.Item.icon != null ? Color.white : slot.Item.iconColor;
        }

        if (_detailName != null) _detailName.text = slot.Item.displayName;
        if (_detailType != null)
            _detailType.text = $"{slot.Item.itemType.GetDisplayName()} · {slot.Count} / {slot.Item.maxStack}";
        if (_detailDesc != null)
            _detailDesc.text = string.IsNullOrEmpty(slot.Item.description) ? "（暂无描述）" : slot.Item.description;
    }

    private void ClearDetail()
    {
        _detailIndex = -1;

        if (_detailIcon != null) _detailIcon.enabled = false;
        if (_detailName != null) _detailName.text = "未选择";
        if (_detailType != null) _detailType.text = string.Empty;
        if (_detailDesc != null) _detailDesc.text = "把鼠标悬停在物品上查看详情。";
    }

    // ==================== 拖拽 ====================

    /// <summary>槽位开始拖拽（由 InventorySlotView 调用）</summary>
    public void BeginDragFromSlot(int index)
    {
        if (_inventory == null) return;

        InventorySlot slot = _inventory.GetSlot(index);
        if (slot == null || slot.IsEmpty) return;

        int count = slot.Count;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            count = Mathf.Max(1, slot.Count / 2);

        ItemDragContext.Begin(slot.Item, count, _inventory, index);
        ShowDragGhost(slot.Item, count);
    }

    /// <summary>
    /// 外部物品拖入入口（宝箱/商店/装备栏接进来时调用一次即可）。
    /// 调用后鼠标会拖着这件物品，放进任意槽位即完成转移。
    /// </summary>
    public void BeginExternalDrag(ItemDefinition item, int count)
    {
        if (item == null || count <= 0) return;

        if (!_isOpen) Open();
        ItemDragContext.BeginExternal(item, count);
        ShowDragGhost(item, count);
    }

    /// <summary>松开鼠标：无论放没放下，都清空上下文（放下成功时已由 HandleDrop 提交数据）</summary>
    public void EndDrag()
    {
        if (_dragGhost != null) _dragGhost.gameObject.SetActive(false);
        ItemDragContext.Clear();
    }

    public void MoveDragGhost(Vector2 screenPosition)
    {
        if (_dragGhost == null || _canvas == null) return;

        RectTransform canvasRect = (RectTransform)_canvas.transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 local))
            _dragGhost.anchoredPosition = local;
    }

    /// <summary>槽位放下：把拖拽载荷提交到目标槽位</summary>
    public void HandleDrop(int targetIndex)
    {
        ItemDragContext.Payload payload = ItemDragContext.Current;
        if (payload == null || _inventory == null) return;

        // 拖回自己身上：什么都不做
        if (!payload.IsExternal && payload.Source == _inventory && payload.SourceIndex == targetIndex)
            return;

        if (payload.IsExternal)
        {
            int placed = _inventory.PlaceInto(targetIndex, payload.Item, payload.Count);
            if (placed < payload.Count) _inventory.TryAdd(payload.Item, payload.Count - placed);
        }
        else
        {
            Inventory.Transfer(payload.Source, payload.SourceIndex, _inventory, targetIndex, payload.Count);
        }

        EndDrag();
    }

    /// <summary>右键拆分（由槽位调用）</summary>
    public void RequestSplit(int index)
    {
        if (_inventory == null) return;
        if (!_inventory.SplitStack(index))
            Debug.Log("[InventoryUI] 拆分失败：数量不足或没有空槽。", this);
    }

    private void ShowDragGhost(ItemDefinition item, int count)
    {
        if (_dragGhost == null || _dragGhostIcon == null) return;

        _dragGhost.gameObject.SetActive(true);
        _dragGhost.SetAsLastSibling();

        _dragGhostIcon.sprite = item.icon != null ? item.icon : InventoryUIFactory.WhiteSprite;
        _dragGhostIcon.color = item.icon != null ? Color.white : item.iconColor;
        if (_dragGhostCount != null) _dragGhostCount.text = count > 1 ? count.ToString() : string.Empty;

        MoveDragGhost(Input.mousePosition);
    }

    // ==================== 构建界面 ====================

    private void BuildUI()
    {
        // 独立 Canvas：不依赖场景里的 GameFlowCanvas（其缩放配置可能被改动）
        var canvasGo = new GameObject("InventoryCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        _canvasRoot = canvasGo;

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = CurrentFont;

        // 半透明遮罩：挡住界面下方的点击，避免误触场景
        Image scrim = InventoryUIFactory.CreateImage("Scrim", canvasGo.transform,
                                                     new Color(0f, 0f, 0f, 0.55f), InventoryUIFactory.WhiteSprite);
        scrim.raycastTarget = true;
        InventoryUIFactory.Stretch(scrim.rectTransform, 0f, 0f, 0f, 0f);

        // 主面板
        Image panelImage = InventoryUIFactory.CreateImage("Panel", canvasGo.transform, panelColor,
                                                          InventoryUIFactory.RoundedSprite, true);
        panelImage.raycastTarget = true;
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(1000f, 620f);
        panel.anchoredPosition = Vector2.zero;

        // 标题
        Text title = InventoryUIFactory.CreateText("Title", panel, font, "背包", 30, textColor, TextAnchor.MiddleLeft);
        InventoryUIFactory.PlaceTopLeft(title.rectTransform, 32f, 16f, 300f, 44f);

        // 容量
        _capacityText = InventoryUIFactory.CreateText("Capacity", panel, font, "0 / 0", 18,
                                                      mutedTextColor, TextAnchor.MiddleRight);
        InventoryUIFactory.PlaceTopRight(_capacityText.rectTransform, 90f, 20f, 160f, 36f);

        // 关闭按钮
        Button closeButton = InventoryUIFactory.CreateButton(panel, "CloseButton", font, "×",
                                                             new Color(0.20f, 0.21f, 0.25f, 1f), textColor, 26,
                                                             out _);
        InventoryUIFactory.PlaceTopRight((RectTransform)closeButton.transform, 24f, 16f, 44f, 44f);
        closeButton.onClick.AddListener(Close);

        // 分割线
        Image divider = InventoryUIFactory.CreateImage("Divider", panel, new Color(1f, 1f, 1f, 0.10f),
                                                       InventoryUIFactory.WhiteSprite);
        InventoryUIFactory.PlaceTopLeft(divider.rectTransform, 28f, 68f, 944f, 2f);

        BuildGrid(panel, font);
        BuildDetail(panel, font);
        BuildBottomBar(panel, font);
        BuildDragGhost();

        _canvasRoot.SetActive(false);
    }

    private void BuildGrid(RectTransform panel, Font font)
    {
        // ScrollRect 容器（背景 + 裁剪 + 滚动）
        Image scrollBg = InventoryUIFactory.CreateImage("GridScroll", panel, new Color(0f, 0f, 0f, 0.20f),
                                                        InventoryUIFactory.RoundedSprite, true);
        scrollBg.raycastTarget = true; // 让空白区域也能响应滚轮（ScrollRect 需要可命中的图形）
        RectTransform scrollRectTransform = scrollBg.rectTransform;
        InventoryUIFactory.PlaceTopLeft(scrollRectTransform, 28f, 84f, 640f, 440f);

        RectTransform viewport = InventoryUIFactory.CreateRect("Viewport", scrollRectTransform);
        InventoryUIFactory.Stretch(viewport, 2f, 2f, 2f, 2f);
        viewport.gameObject.AddComponent<RectMask2D>();

        _gridContent = InventoryUIFactory.CreateRect("Content", viewport);
        _gridContent.anchorMin = new Vector2(0f, 1f);
        _gridContent.anchorMax = new Vector2(1f, 1f);
        _gridContent.pivot = new Vector2(0.5f, 1f);
        _gridContent.anchoredPosition = Vector2.zero;
        _gridContent.sizeDelta = Vector2.zero;

        var grid = _gridContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(92f, 92f);
        grid.spacing = new Vector2(10f, 10f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.childAlignment = TextAnchor.UpperLeft;

        var fitter = _gridContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollBg.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = _gridContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    private void BuildDetail(RectTransform panel, Font font)
    {
        Image detail = InventoryUIFactory.CreateImage("Detail", panel, new Color(1f, 1f, 1f, 0.05f),
                                                      InventoryUIFactory.RoundedSprite, true);
        RectTransform detailRect = detail.rectTransform;
        InventoryUIFactory.PlaceTopLeft(detailRect, 692f, 84f, 280f, 440f);

        _detailIcon = InventoryUIFactory.CreateImage("DetailIcon", detailRect, Color.white,
                                                     InventoryUIFactory.WhiteSprite);
        RectTransform iconRect = _detailIcon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -24f);
        iconRect.sizeDelta = new Vector2(84f, 84f);
        _detailIcon.enabled = false;

        _detailName = InventoryUIFactory.CreateText("DetailName", detailRect, font, "未选择", 22,
                                                     textColor, TextAnchor.MiddleCenter);
        InventoryUIFactory.PlaceTopLeft(_detailName.rectTransform, 16f, 120f, 248f, 32f);

        _detailType = InventoryUIFactory.CreateText("DetailType", detailRect, font, string.Empty, 15,
                                                     mutedTextColor, TextAnchor.MiddleCenter);
        InventoryUIFactory.PlaceTopLeft(_detailType.rectTransform, 16f, 154f, 248f, 26f);

        _detailDesc = InventoryUIFactory.CreateText("DetailDesc", detailRect, font,
                                                     "把鼠标悬停在物品上查看详情。", 16,
                                                     mutedTextColor, TextAnchor.UpperLeft);
        InventoryUIFactory.PlaceTopLeft(_detailDesc.rectTransform, 20f, 196f, 240f, 220f);
    }

    private void BuildBottomBar(RectTransform panel, Font font)
    {
        Button sortByType = InventoryUIFactory.CreateButton(panel, "SortByType", font, "按类型整理",
                                                             accentColor, new Color(0.05f, 0.10f, 0.07f, 1f), 18,
                                                             out _);
        InventoryUIFactory.PlaceBottomLeft((RectTransform)sortByType.transform, 28f, 24f, 150f, 46f);
        sortByType.onClick.AddListener(() => _inventory?.Sort(InventorySortMode.ByType));

        Button sortByCount = InventoryUIFactory.CreateButton(panel, "SortByCount", font, "按数量整理",
                                                              new Color(0.24f, 0.26f, 0.32f, 1f), textColor, 18,
                                                              out _);
        InventoryUIFactory.PlaceBottomLeft((RectTransform)sortByCount.transform, 190f, 24f, 150f, 46f);
        sortByCount.onClick.AddListener(() => _inventory?.Sort(InventorySortMode.ByCount));

        Text hint = InventoryUIFactory.CreateText("Hint", panel, font,
                                                   "左键拖拽：交换 / 合并　Shift+拖拽：拆一半　右键：拆分一半", 15,
                                                   mutedTextColor, TextAnchor.MiddleLeft);
        InventoryUIFactory.PlaceBottomLeft(hint.rectTransform, 360f, 24f, 580f, 46f);
    }

    private void BuildDragGhost()
    {
        var ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
        ghostGo.transform.SetParent(_canvas.transform, false);

        _dragGhost = (RectTransform)ghostGo.transform;
        _dragGhost.anchorMin = _dragGhost.anchorMax = new Vector2(0.5f, 0.5f);
        _dragGhost.pivot = new Vector2(0.5f, 0.5f);
        _dragGhost.sizeDelta = new Vector2(72f, 72f);

        _dragGhostIcon = ghostGo.GetComponent<Image>();
        _dragGhostIcon.sprite = InventoryUIFactory.WhiteSprite;
        _dragGhostIcon.type = Image.Type.Sliced;
        _dragGhostIcon.raycastTarget = false; // 不能挡住槽位的 OnDrop

        _dragGhostCount = InventoryUIFactory.CreateText("Count", ghostGo.transform, CurrentFont, string.Empty,
                                                        18, textColor, TextAnchor.LowerRight);
        InventoryUIFactory.Stretch(_dragGhostCount.rectTransform, 4f, 4f, 2f, 2f);

        ghostGo.SetActive(false);
    }
}
