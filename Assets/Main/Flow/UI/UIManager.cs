using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 层总控（全局唯一）—— 所有 GamePanel 显隐的唯一仲裁者。
///
/// 工作方式（注册制 + 状态互斥）：
///   1. 面板在 OnEnable 时向本管理器注册（GamePanel 自动完成，无需手动拖引用）；
///   2. 本管理器订阅 GameManager.OnStateChanged，状态一变即 RefreshAll：
///      只有 ShownState 与当前状态匹配的面板显示，其余全部隐藏 —— 结构上保证
///      "同一时间只有一个全屏流程面板"，杜绝面板堆叠/残留；
///   3. 配置诊断：两个面板声明同一个 ShownState 时立即告警，把配置错误消灭在运行初期。
///
/// 层级/优先级约定：
///   sortingPriority 仅作为注册表排序依据与未来"同屏叠加面板"（如 HUD + 弹窗）的扩展点；
///   当前流程面板互斥显示，实际绘制顺序由 Hierarchy 兄弟顺序决定（见使用说明的层级表）。
///
/// 依赖方向：UIManager → GameManager（读状态）；GamePanel → UIManager（注册/受控）。
/// GameManager 永远不引用任何 UI —— 逻辑层与表现层单向依赖。
/// </summary>
[DefaultExecutionOrder(-90)] // 晚于 GameManager(-100) 初始化、早于各面板(0)注册，订阅/注册时序安全
[AddComponentMenu("RPG/GameFlow/UI Manager")]
public class UIManager : MonoBehaviour
{
    private static UIManager _instance;

    /// <summary>单例访问点（惰性查找兜底，规避脚本执行顺序问题）</summary>
    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<UIManager>();
            return _instance;
        }
    }

    /// <summary>已注册面板（按 sortingPriority 降序维护）</summary>
    private readonly List<GamePanel> _panels = new List<GamePanel>();

    void OnEnable()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[UIManager] 场景中找不到 GameManager，UI 总控无法响应流程状态变化。", this);
            return;
        }
        gm.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.OnStateChanged -= HandleStateChanged;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ==================== 面板注册（GamePanel 自动调用） ====================

    public void Register(GamePanel panel)
    {
        if (panel == null || _panels.Contains(panel)) return;

        _panels.Add(panel);
        _panels.Sort((a, b) => b.SortingPriority.CompareTo(a.SortingPriority)); // 高优先级在前

        // 配置诊断：同状态多面板 = 运行期堆叠，提前告警定位
        for (int i = 0; i < _panels.Count; i++)
        {
            var other = _panels[i];
            if (other != panel && other.ShownState == panel.ShownState)
                Debug.LogWarning(
                    $"[UIManager] 面板 {other.name} 与 {panel.name} 都声明显示于 {panel.ShownState} 状态，" +
                    "会同时出现，请检查其中一个的 ShownState。", this);
        }

        // 注册即对齐：按当前流程状态立即同步显隐（覆盖中途激活/动态创建的面板）
        var gm = GameManager.Instance;
        GameState current = gm != null ? gm.State : GameState.MainMenu;
        panel.SetVisible(panel.ShownState == current, instant: true);
    }

    public void Unregister(GamePanel panel)
    {
        if (panel != null) _panels.Remove(panel);
    }

    // ==================== 状态驱动刷新 ====================

    private void HandleStateChanged(GameState previous, GameState current) => RefreshAll();

    /// <summary>
    /// 统一刷新所有已注册面板：与当前状态匹配者显示，其余隐藏（互斥，无残留）。
    /// public 便于在自定义逻辑（如过场动画结束）后手动触发一次对齐。
    /// </summary>
    public void RefreshAll()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        for (int i = 0; i < _panels.Count; i++)
            _panels[i].SetVisible(_panels[i].ShownState == gm.State, instant: false);
    }
}
