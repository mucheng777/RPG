using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始页面面板。
///
/// 素材替换扩展点：
///   本脚本只依赖 RectTransform / CanvasGroup / Button 这些原生组件，
///   标题文字、背景图、按钮美术、动效全部在编辑器里替换即可，脚本零改动。
///   想加"设置/继续/制作人员"等按钮：在 OnEnable/OnDisable 里照抄 AddListener/RemoveListener 模式即可。
/// </summary>
[AddComponentMenu("RPG/GameFlow/UI/Main Menu Panel")]
public class MainMenuPanel : GamePanel
{
    [Header("按钮（可留空，不填则该按钮不生效）")]
    [Tooltip("开始游戏按钮")]
    [SerializeField] private Button startButton;

    [Tooltip("退出游戏按钮")]
    [SerializeField] private Button quitButton;

    public override GameState ShownState => GameState.MainMenu;

    protected override void OnEnable()
    {
        base.OnEnable(); // 先向 UIManager 注册

        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
    }

    protected override void OnDisable()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);

        base.OnDisable(); // 后向 UIManager 注销
    }

    private void OnStartClicked()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.StartGame();
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 编辑器内停止播放
#else
        Application.Quit();
#endif
    }
}
