using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏流程管理器 —— 全局流程状态的唯一真相源。
///
/// 职责（单一职责，不越界）：
///   1. 持有并切换 GameState；广播 OnStateChanged；
///   2. 把"状态"翻译成"环境副作用"：Time.timeScale / AudioListener.pause / 光标锁定；
///   3. 暴露意图方法（StartGame / TogglePause / RespawnPlayer / RestartGame ...），
///      UI 只调用这些方法，不自己改状态、不碰 Time.timeScale。
///   不负责：玩家血量与位移（交给 PlayerLifecycleBridge / PlayerHealth），
///           UI 的显隐与布局（交给 GamePanel 子类）—— 低耦合。
///
/// 扩展点：
///   - 新增流程状态：在 GameState 追加枚举 + 在 ApplyState 补一条副作用规则；
///   - 新增面板：继承 GamePanel 声明 ShownState 即可，本类无需改动；
///   - 多场景扩展：把 RestartGame/ReturnToMainMenu 换成 SceneManager.LoadScene(场景名) 即可，
///     其余流程逻辑不变。
/// </summary>
[DefaultExecutionOrder(-100)] // 确保 Awake/Instance 早于其它脚本的 OnEnable，面板订阅才安全
[AddComponentMenu("RPG/GameFlow/Game Manager")]
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;

    /// <summary>
    /// 单例访问点。采用"惰性查找"兜底：即使某面板的 OnEnable 早于本组件 Awake，
    /// 也能通过 FindFirstObjectByType 拿到实例，避免脚本执行顺序带来的隐性 bug。
    /// </summary>
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<GameManager>();
            return _instance;
        }
    }

    [Header("配置")]
    [Tooltip("流程配置；留空则运行时使用内置默认值（开箱即跑，后续可创建资产替换）")]
    [SerializeField] private GameFlowProfile profile;

    /// <summary>状态切换事件（上一个状态, 当前状态）。UI 订阅它刷新，禁止每帧轮询。</summary>
    public event System.Action<GameState, GameState> OnStateChanged;

    private GameState _state;
    private PlayerLifecycleBridge _player;
    private float _respawnTimer;

    /// <summary>
    /// 重载场景后要进入的状态。
    /// 场景重载会重建 GameManager，Awake 时需要用 startState 初始化，
    /// 但 RestartGame 与 ReturnToMainMenu 都走 LoadScene，必须靠此静态标记区分意图。
    /// 静态字段在 SceneManager.LoadScene 后保留（同域不重载），仅在首次进入 Play 时为 null。
    /// </summary>
    private static GameState? _pendingStateAfterReload;

    public GameState State => _state;
    public GameFlowProfile Profile => profile;
    public bool IsPlaying => _state == GameState.Playing;
    public bool IsPaused => _state == GameState.Paused;
    public bool IsPlayerDead => _state == GameState.PlayerDead;

    // ==================== 生命周期 ====================

    void Awake()
    {
        // 单例守卫：重复实例直接销毁，避免多份流程状态互相打架
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[GameManager] 场景中存在多个 GameManager，已销毁重复实例。", this);
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (profile == null)
            profile = ScriptableObject.CreateInstance<GameFlowProfile>();

        // 重载场景后恢复意图状态（RestartGame→Playing，ReturnToMainMenu→MainMenu）；
        // 首次进入 Play 时无 pending，使用 startState。
        if (_pendingStateAfterReload.HasValue)
        {
            _state = _pendingStateAfterReload.Value;
            _pendingStateAfterReload = null;
        }
        else
        {
            _state = profile.startState;
        }
        ApplyState(_state);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        // 自动复活倒计时。死亡期间 timeScale=0，必须用 unscaledDeltaTime，否则永远数不到 0
        if (_state == GameState.PlayerDead && profile.autoRespawnDelay > 0f)
        {
            _respawnTimer -= Time.unscaledDeltaTime;
            if (_respawnTimer <= 0f) RespawnPlayer();
        }
    }

    void LateUpdate()
    {
        // 每帧收敛光标状态：
        // 项目里 ThirdPersonCamera.OnEnable 与 CursorLockManager 会在初始化/点击时抢占光标，
        // 放在 LateUpdate 里统一收口，保证"菜单里光标可用、游戏中光标锁定"始终成立。
        ApplyCursor();
    }

    // ==================== 对外意图接口（UI 按钮只调这些） ====================

    /// <summary>开始游戏（MainMenu → Playing）</summary>
    public void StartGame()
    {
        if (_state == GameState.Playing) return;
        SetState(GameState.Playing);
    }

    /// <summary>暂停 / 继续 切换（Playing ⇄ Paused）</summary>
    public void TogglePause()
    {
        if (_state == GameState.Playing) SetState(GameState.Paused);
        else if (_state == GameState.Paused) SetState(GameState.Playing);
    }

    /// <summary>继续游戏（仅从 Paused 生效）</summary>
    public void Resume()
    {
        if (_state == GameState.Paused) SetState(GameState.Playing);
    }

    /// <summary>玩家死亡通知入口（由 PlayerLifecycleBridge 在 OnDied 时调用）</summary>
    public void NotifyPlayerDied()
    {
        if (_state == GameState.PlayerDead) return;
        SetState(GameState.PlayerDead);
    }

    /// <summary>复活玩家（PlayerDead → Playing）：先复位玩家，再切回运行状态</summary>
    public void RespawnPlayer()
    {
        if (_state != GameState.PlayerDead) return;

        var player = Player;
        if (player != null) player.RevivePlayer();

        SetState(GameState.Playing);
    }

    /// <summary>重新开始：默认重载当前场景彻底重置世界；也可配置为仅原地重置玩家</summary>
    public void RestartGame()
    {
        if (profile.restartReloadsScene)
        {
            ReloadActiveScene(GameState.Playing);
            return;
        }

        var player = Player;
        if (player != null) player.RevivePlayer();
        SetState(GameState.Playing);
    }

    /// <summary>返回主菜单：重载场景后回到 startState(MainMenu)，形成"开始→游玩→死亡→重开"闭环</summary>
    public void ReturnToMainMenu()
    {
        if (profile.restartReloadsScene)
        {
            ReloadActiveScene(GameState.MainMenu);
            return;
        }

        SetState(GameState.MainMenu);
    }

    // ==================== 玩家注册（解耦：管理器不认识玩家内部结构） ====================

    public void RegisterPlayer(PlayerLifecycleBridge player) => _player = player;

    public void UnregisterPlayer(PlayerLifecycleBridge player)
    {
        if (_player == player) _player = null;
    }

    private PlayerLifecycleBridge Player
    {
        // 注册优先；未注册时惰性查找一次（兜底：玩家挂载桥组件但不注册的情况）
        get { return _player != null ? _player : (_player = FindFirstObjectByType<PlayerLifecycleBridge>()); }
    }

    // ==================== 内部实现 ====================

    private void SetState(GameState next)
    {
        if (_state == next) return;

        GameState previous = _state;
        _state = next;

        ApplyState(next);
        OnStateChanged?.Invoke(previous, next); // 先应用副作用再广播，订阅者拿到的环境已是新状态
    }

    /// <summary>把流程状态翻译为环境副作用（时间/音频/光标/复活计时）</summary>
    private void ApplyState(GameState state)
    {
        bool playing = state == GameState.Playing;

        // 时间：仅 Playing 正常流动，其余状态冻结世界（敌人/玩家/动画全部暂停）
        Time.timeScale = playing ? 1f : 0f;

        // 音频
        if (profile.pauseAudioListener)
            AudioListener.pause = !playing;

        // 光标（立即应用一次，LateUpdate 每帧维持）
        ApplyCursor();

        // 进入死亡态时启动自动复活计时
        if (state == GameState.PlayerDead)
            _respawnTimer = profile.autoRespawnDelay;
    }

    private void ApplyCursor()
    {
        bool lockCursor = _state == GameState.Playing && profile.lockCursorWhilePlaying;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;
    }

    /// <summary>
    /// 重载当前场景。
    /// </summary>
    /// <param name="stateAfterReload">重载后进入的流程状态（由调用方指定：重开→Playing，回主菜单→MainMenu）</param>
    private void ReloadActiveScene(GameState stateAfterReload)
    {
        // 重载前恢复时间与音频，否则新场景会带着 timeScale=0 初始化
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // 传递重载后意图状态，供新场景 GameManager.Awake 消费
        _pendingStateAfterReload = stateAfterReload;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
