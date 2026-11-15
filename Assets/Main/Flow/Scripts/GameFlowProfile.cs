using UnityEngine;

/// <summary>
/// 游戏流程配置（ScriptableObject）。
/// 遵循项目惯例：配置与逻辑分离，策划可在 Inspector 调参，代码零改动。
///
/// 兼容策略：GameManager 未指定本资产时会在运行时创建一份带默认值的实例，
/// 因此"不建资产也能直接跑"；后续想调参，只需 Create → RPG → Game Flow Profile 并拖入。
/// </summary>
[CreateAssetMenu(fileName = "GameFlowProfile", menuName = "RPG/Game Flow Profile")]
public class GameFlowProfile : ScriptableObject
{
    [Header("初始状态")]
    [Tooltip("进入场景后的初始流程状态。做成 MainMenu = 先看到开始页；改为 Playing = 直接进游戏")]
    public GameState startState = GameState.MainMenu;

    [Header("输入")]
    [Tooltip("暂停 / 继续按键")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("暂停")]
    [Tooltip("暂停时是否暂停全局音频（AudioListener.pause）")]
    public bool pauseAudioListener = true;

    [Header("光标")]
    [Tooltip("Playing 状态下是否锁定并隐藏光标")]
    public bool lockCursorWhilePlaying = true;

    [Header("死亡与复活")]
    [Tooltip("死亡后自动复活延迟（秒）；<= 0 表示不自动复活，仅由死亡面板按钮手动复活")]
    public float autoRespawnDelay = 0f;

    [Header("重新开始 / 返回主菜单")]
    [Tooltip("是否通过重载当前场景来彻底重置世界；false 则仅原地重置玩家（不重置敌人等场景物体）")]
    public bool restartReloadsScene = true;
}
