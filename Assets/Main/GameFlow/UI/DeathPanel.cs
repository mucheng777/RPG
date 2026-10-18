using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 死亡提示面板：原地复活 / 重新开始 / 返回主菜单。
/// 玩家血量归零时由 GameManager 切入 PlayerDead 状态，本面板自动淡入。
/// </summary>
[AddComponentMenu("RPG/GameFlow/UI/Death Panel")]
public class DeathPanel : GamePanel
{
    [Header("按钮")]
    [Tooltip("复活（原地满血 + 回到复活点）")]
    [SerializeField] private Button respawnButton;

    [Tooltip("重新开始（重载场景，彻底重置世界）")]
    [SerializeField] private Button restartButton;

    [Tooltip("返回主菜单")]
    [SerializeField] private Button mainMenuButton;

    public override GameState ShownState => GameState.PlayerDead;

    protected override void OnEnable()
    {
        base.OnEnable(); // 先向 UIManager 注册

        if (respawnButton != null) respawnButton.onClick.AddListener(OnRespawnClicked);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    protected override void OnDisable()
    {
        if (respawnButton != null) respawnButton.onClick.RemoveListener(OnRespawnClicked);
        if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);

        base.OnDisable(); // 后向 UIManager 注销
    }

    private void OnRespawnClicked()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.RespawnPlayer();
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
