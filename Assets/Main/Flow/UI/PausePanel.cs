using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 暂停面板：继续 / 重新开始 / 返回主菜单。
/// 与 MainMenuPanel 同构：只把按钮点击翻译成 GameManager 的意图调用。
/// </summary>
[AddComponentMenu("RPG/GameFlow/UI/Pause Panel")]
public class PausePanel : GamePanel
{
    [Header("按钮")]
    [Tooltip("继续游戏")]
    [SerializeField] private Button resumeButton;

    [Tooltip("重新开始（重载场景，彻底重置世界）")]
    [SerializeField] private Button restartButton;

    [Tooltip("返回主菜单")]
    [SerializeField] private Button mainMenuButton;

    public override GameState ShownState => GameState.Paused;

    protected override void OnEnable()
    {
        base.OnEnable(); // 先向 UIManager 注册

        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    protected override void OnDisable()
    {
        if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeClicked);
        if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);

        base.OnDisable(); // 后向 UIManager 注销
    }

    private void OnResumeClicked()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.Resume();
    }

    private void OnRestartClicked()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.RestartGame();
    }

    private void OnMainMenuClicked()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.ReturnToMainMenu();
    }
}
