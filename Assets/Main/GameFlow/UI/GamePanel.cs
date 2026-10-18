using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏流程面板基类（UI 表现层，零业务逻辑）。
///
/// v2 关键变更（修复"点开始游戏后主菜单不消失"）：
///   1. CanvasGroup 自动获取/自动添加 —— 旧版必须手动拖引用，漏拖则隐藏逻辑静默失效；
///   2. 显隐仲裁权上交 UIManager：面板不再自行订阅 GameManager，
///      由 UI 总控按流程状态统一刷新（同一时间只有一个面板可见，结构上杜绝堆叠）；
///   3. 新增图片占位适配接口（SetBackgroundSprite / SetBackgroundColor），
///      当前用原生 Image 纯色占位，接入美术素材零改码。
///
/// 职责边界（面试讲解点）：
///   - 面板只声明"我在哪个状态显示"（ShownState），不决定自己何时显隐；
///   - 子类按钮回调只调用 GameManager 的意图方法（StartGame/TogglePause/...），
///     面板自己不修改状态、不碰 Time.timeScale —— 状态唯一真相源在 GameManager；
///   - 新增流程面板只需继承本类并重写 ShownState，GameManager/UIManager 零改动（开闭原则）。
///
/// 注意：面板物体必须保持常驻激活（勿手动 SetActive(false)），
/// 显隐完全由 CanvasGroup 的 alpha/交互开关控制，事件订阅与淡入淡出才稳定。
///
/// 过渡动画使用 Time.unscaledDeltaTime：暂停/死亡时 timeScale=0，普通 deltaTime 为 0 会卡住。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public abstract class GamePanel : MonoBehaviour
{
    [Header("显隐")]
    [Tooltip("淡入淡出用的 CanvasGroup。留空会自动获取，没有则自动添加（无需手动拖）")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("显隐过渡速度（越大越快）")]
    [SerializeField] private float fadeSpeed = 10f;

    [Tooltip("层级优先级：数值越大越靠上层。当前流程面板互斥显示（同屏仅一个），此值为未来叠加面板预留")]
    [SerializeField] private int sortingPriority = 0;

    [Header("占位视觉（美术接入适配点）")]
    [Tooltip("背景图。当前用纯色占位；接入美术时在 Inspector 拖 Sprite 或运行时调 SetBackgroundSprite")]
    [SerializeField] private Image backgroundImage;

    /// <summary>本面板在哪个游戏状态下显示（由子类重写，避免 Inspector 配错）</summary>
    public abstract GameState ShownState { get; }

    /// <summary>对外只读：当前是否处于显示状态</summary>
    public bool IsVisible { get; private set; }

    /// <summary>层级优先级（UIManager 注册表排序用）</summary>
    public int SortingPriority => sortingPriority;

    private float _targetAlpha;

    protected virtual void Awake()
    {
        // 关键修复：CanvasGroup 保证存在 —— 自动获取；没有则自动添加并提示。
        // 旧版漏拖引用时隐藏逻辑静默失效，是"面板残留"问题的根因。
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                Debug.LogWarning($"[GamePanel] {name} 缺少 CanvasGroup，已自动添加" +
                                 "（建议在编辑器里手动加上以明确结构）。", this);
            }
        }

        // 初始全透明、不可交互：等 UIManager 注册时按当前状态立即对齐，避免开屏闪面板
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    protected virtual void OnEnable()
    {
        var ui = UIManager.Instance;
        if (ui == null)
        {
            Debug.LogError($"[GamePanel] {name}: 场景中找不到 UIManager（UI 层总控）。" +
                           "请在 UI 根物体上挂 UIManager 组件，否则面板无法显示。", this);
            return;
        }

        ui.Register(this); // 注册时 UIManager 会立即按当前游戏状态同步本面板显隐
    }

    protected virtual void OnDisable()
    {
        var ui = UIManager.Instance;
        if (ui != null) ui.Unregister(this);
    }

    protected virtual void Update()
    {
        if (Mathf.Approximately(canvasGroup.alpha, _targetAlpha)) return;

        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 显隐控制入口（仅供 UIManager 调用，业务代码不要直接调，保证仲裁唯一）。
    /// </summary>
    public void SetVisible(bool visible, bool instant)
    {
        bool changed = IsVisible != visible;
        IsVisible = visible;

        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        _targetAlpha = visible ? 1f : 0f;
        if (instant) canvasGroup.alpha = _targetAlpha;

        if (changed)
        {
            if (visible) OnShown();
            else OnHidden();
        }
    }

    // ==================== 图片资源接入适配接口（占位 → 正式素材零改码） ====================

    /// <summary>
    /// 替换背景图（运行时换肤/主题切换用；编辑器里直接往 backgroundImage 拖 Sprite 亦可）。
    /// </summary>
    public void SetBackgroundSprite(Sprite sprite)
    {
        if (backgroundImage != null) backgroundImage.sprite = sprite;
    }

    /// <summary>调整背景占位色/透明度（纯色占位阶段用，如死亡面板调暗红）</summary>
    public void SetBackgroundColor(Color color)
    {
        if (backgroundImage != null) backgroundImage.color = color;
    }

    /// <summary>面板显示时回调（扩展点：播放入场音效/动画）</summary>
    protected virtual void OnShown() { }

    /// <summary>面板隐藏时回调（扩展点：停用交互、释放资源）</summary>
    protected virtual void OnHidden() { }
}
