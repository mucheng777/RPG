/// <summary>
/// 游戏全局流程状态（把"游戏现在处于哪个阶段"收敛为单一枚举）。
///
/// 设计要点（面试讲解点）：
///   1. 单一真相源：全局流程只用一个状态枚举表达，杜绝各处散落 bool 标志位
///      （如 isPaused / isDead / isMenuOpen）导致的状态组合不一致。
///   2. 谁关心、谁订阅：UI 面板、玩家生命周期桥通过 GameManager.OnStateChanged 响应，
///      不每帧轮询，也不各自维护"我该不该显示"的副本。
///   3. 可扩展：以后加"过场/结算/商店"等流程，只需在此追加枚举 + 在 GameManager 状态
///      应用处补一条规则，既有状态逻辑不受影响（开闭原则）。
///
/// 状态流转：
///   MainMenu ──开始游戏──▶ Playing ⇄ Paused
///   Playing  ──玩家死亡──▶ PlayerDead ──复活──▶ Playing
///   任意状态 ──重新开始/返回主菜单──▶ 重载当前场景（回到 startState）
/// </summary>
public enum GameState
{
    /// <summary>开始页面：世界冻结，只显示开始菜单</summary>
    MainMenu,

    /// <summary>游戏运行：时间正常流动，光标锁定，玩家可操作</summary>
    Playing,

    /// <summary>游戏暂停：时间冻结（timeScale=0），显示暂停菜单</summary>
    Paused,

    /// <summary>玩家死亡：时间冻结，显示死亡面板，等待复活/重开</summary>
    PlayerDead
}
