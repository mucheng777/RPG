using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包界面（表现层）—— 只做"展示 + 交互翻译"，所有数据变更都通过 Inventory 的公开 API 提交。
///
/// 结构（界面全部在 Hierarchy 里搭建，本脚本只做接线）：
///   InventorySystem（挂本脚本）→ 引用 Hierarchy 里的 Canvas 根节点；
///   开关背包 = 显隐这个 Canvas 根节点，运行时不创建任何控件。
///
/// 交互：
///   - Tab / I：打开、关闭背包
///   - 左键拖拽：移动到空槽 / 同类合并 / 不同类交换
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

    [Header("按键")]
    [Tooltip("开关背包按键")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Tooltip("备用开关按键")]
    [SerializeField] private KeyCode alternateToggleKey = KeyCode.I;

    [Header("行为")]
    [Tooltip("打开背包时冻结游戏时间")]
    [SerializeField] private bool pauseWhileOpen = true;

    [Tooltip("打开背包时禁用玩家控制（避免点击 UI 同时挥砍）")]
    [SerializeField] private bool blockPlayerInputWhileOpen = true;

    [Header("界面引用（必填：把 Hierarchy 里的控件拖进来）")]
    [Tooltip("整个背包界面的根节点（挂 Canvas 的物体）。开关背包时只显隐它，" +
             "所以本脚本要挂在另一个常驻物体上，不能和它同一个物体")]
    [SerializeField] private GameObject uiRoot;

    [Tooltip("槽位父节点：必须挂 GridLayoutGroup，且不要预放示例槽位")]
    [SerializeField] private RectTransform gridContent;

    [Tooltip("容量文字，例如 3 / 24")]
    [SerializeField] private TMP_Text capacityText;

    [Tooltip("详情图标")]
    [SerializeField] private Image detailIcon;

    [Tooltip("详情标题")]
    [SerializeField] private TMP_Text detailName;

    [Tooltip("详情类型/数量")]
    [SerializeField] private TMP_Text detailType;

    [Tooltip("详情描述")]
    [SerializeField] private TMP_Text detailDesc;

    [Tooltip("拖拽时跟随鼠标的图标（要放在 Canvas 下面）")]
    [SerializeField] private RectTransform dragGhost;

    [Tooltip("拖拽图标里的物品图")]
    [SerializeField] private Image dragGhostIcon;

    [Tooltip("拖拽图标里的数量文字")]
    [SerializeField] private TMP_Text dragGhostCount;

    [Tooltip("关闭按钮")]
    [SerializeField] private Button closeButton;

    [Tooltip("按类型整理按钮")]
    [SerializeField] private Button sortByTypeButton;

    [Tooltip("按数量整理按钮")]
    [SerializeField] private Button sortByCountButton;

    [Tooltip("槽位预制体（必填）：每个格子实例化一份")]
    [SerializeField] private InventorySlotView slotPrefab;

    public bool IsOpen => _isOpen;

    private readonly List<InventorySlotView> _views = new List<InventorySlotView>();

    private Inventory _inventory;
    private PlayerController _playerController;

    private Canvas _canvas;
    private GameObject _canvasRoot;
    private RectTransform _gridContent;
    private TMP_Text _capacityText;
    private Image _detailIcon;
    private TMP_Text _detailName;
    private TMP_Text _detailType;
    private TMP_Text _detailDesc;
    private RectTransform _dragGhost;
    private Image _dragGhostIcon;
    private TMP_Text _dragGhostCount;

    private bool _isOpen;
    private int _detailIndex = -1;
    private float _prevTimeScale = 1f;
    private bool _playerInputBlocked;
    private Sprite _detailDefaultSprite;
    private Sprite _dragGhostDefaultSprite;

    // ==================== 生命周期 ====================

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        BindUI();
    }

    void Start()
    {
        _inventory = FindFirstObjectByType<Inventory>();
        if (_inventory == null)
        {
            Debug.LogError("[InventoryUI] 场景中找不到 Inventory（背包）组件，请确认背包数据所在的物体已挂 Inventory。", this);
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
        if (slotPrefab == null)
        {
            Debug.LogError("[InventoryUI] 没有指定槽位预制体（slotPrefab），背包里不会出现格子。", this);
            return null;
        }

        InventorySlotView view = Instantiate(slotPrefab, _gridContent);
        view.gameObject.name = $"Slot_{index:00}";
        view.gameObject.SetActive(true);
        view.Bind(this, index);
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
            // 没有图标就显示预制体里自带的占位图，再用物品颜色染色
            _detailIcon.sprite = slot.Item.icon != null ? slot.Item.icon : _detailDefaultSprite;
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

    /// <summary>拖拽跟随图标</summary>
    private void ShowDragGhost(ItemDefinition item, int count)
    {
        if (_dragGhost == null || _dragGhostIcon == null) return;

        _dragGhost.gameObject.SetActive(true);
        _dragGhost.SetAsLastSibling();

        // 跟随图标绝不能参与射线检测：否则它会挡住指针下方的槽位，导致 OnDrop 收不到、放不下去
        foreach (Graphic graphic in _dragGhost.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        _dragGhostIcon.sprite = item.icon != null ? item.icon : _dragGhostDefaultSprite;
        _dragGhostIcon.color = item.icon != null ? Color.white : item.iconColor;
        if (_dragGhostCount != null) _dragGhostCount.text = count > 1 ? count.ToString() : string.Empty;

        MoveDragGhost(Input.mousePosition);
    }

    // ==================== 界面绑定 ====================

    /// <summary>
    /// 把 Inspector 里拖进来的界面引用接到运行时字段上。
    /// 界面完全由 Hierarchy 搭好，这里只做接线，不创建任何控件。
    /// </summary>
    private void BindUI()
    {
        if (uiRoot == null)
        {
            Debug.LogError("[InventoryUI] 没有指定界面根节点（uiRoot），请把 Hierarchy 里的背包 Canvas 拖进来。", this);
            enabled = false;
            return;
        }

        // 脚本挂在界面根节点上时，关闭背包会连带禁用脚本，之后就按不开背包了
        if (uiRoot == gameObject)
        {
            Debug.LogError("[InventoryUI] 界面根节点不能是挂本脚本的物体。请把本脚本挪到一个常驻物体，界面根节点拖到它的引用里。", this);
            enabled = false;
            return;
        }

        _canvasRoot = uiRoot;

        // 拖拽图标需要 Canvas 的 RectTransform 做坐标转换
        _canvas = uiRoot.GetComponent<Canvas>();
        if (_canvas == null) _canvas = uiRoot.GetComponentInParent<Canvas>();

        _gridContent = gridContent;
        _capacityText = capacityText;
        _detailIcon = detailIcon;
        _detailName = detailName;
        _detailType = detailType;
        _detailDesc = detailDesc;
        _dragGhost = dragGhost;
        _dragGhostIcon = dragGhostIcon;
        _dragGhostCount = dragGhostCount;

        // 记住预制体自带的占位图：物品没有图标时回退到它
        if (_detailIcon != null) _detailDefaultSprite = _detailIcon.sprite;
        if (_dragGhostIcon != null) _dragGhostDefaultSprite = _dragGhostIcon.sprite;

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (sortByTypeButton != null)
            sortByTypeButton.onClick.AddListener(() => _inventory?.Sort(InventorySortMode.ByType));
        if (sortByCountButton != null)
            sortByCountButton.onClick.AddListener(() => _inventory?.Sort(InventorySortMode.ByCount));

        if (_dragGhost != null) _dragGhost.gameObject.SetActive(false);
        // 跟随图标不能挡住槽位的 OnDrop，这里强制关掉，避免漏勾
        if (_dragGhostIcon != null) _dragGhostIcon.raycastTarget = false;

        WarnMissingReferences();
        ClearDetail();

        // 初始关闭
        uiRoot.SetActive(false);
    }

    /// <summary>把最容易漏拖的引用一次性提示出来，避免运行后只有一半界面可用</summary>
    private void WarnMissingReferences()
    {
        if (_gridContent == null)
            Debug.LogWarning("[InventoryUI] 未指定槽位父节点（gridContent），背包里不会出现格子。", this);
        if (slotPrefab == null)
            Debug.LogWarning("[InventoryUI] 未指定槽位预制体（slotPrefab），背包里不会出现格子。", this);
        if (_capacityText == null)
            Debug.LogWarning("[InventoryUI] 未指定容量文字（capacityText），容量不会刷新。", this);
        if (closeButton == null)
            Debug.LogWarning("[InventoryUI] 未指定关闭按钮（closeButton）。", this);
        if (_dragGhost == null || _dragGhostIcon == null)
            Debug.LogWarning("[InventoryUI] 未指定拖拽图标（dragGhost / dragGhostIcon），拖拽时看不到跟随图标。", this);
    }
}
