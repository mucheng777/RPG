using UnityEngine;

/// <summary>
/// 玩家生命周期桥（挂在玩家根物体上）——把"玩家血量系统"与"游戏流程系统"解耦地接起来。
///
/// 依赖方向（依赖倒置）：
///   本组件 → IHealthSystem（监听 OnDied） / IRevivable（调用 Revive）
///   不依赖 PlayerHealth 具体类 → 以后换血量实现只要实现这些接口即可，本组件零改动。
///
/// 单一职责：
///   - 死亡：收到 OnDied → 通知 GameManager 切到 PlayerDead；
///   - 复活：GameManager 调用 RevivePlayer() → 触发数据复位(IRevivable) + 空间复位(自身职责)。
///   不修改 Time.timeScale、不管 UI —— 那些是 GameManager / 面板的职责。
///
/// 非侵入：不改动 PlayerController / PlayerLocomotion / PlayerCombat，
/// 死亡期间玩家"无法操作"是由 GameManager 把 timeScale 置 0 顺带达成的。
/// </summary>
[AddComponentMenu("RPG/GameFlow/Player Lifecycle Bridge")]
public class PlayerLifecycleBridge : MonoBehaviour
{
    [Header("复活点")]
    [Tooltip("复活位置/朝向。留空则使用玩家在场景中的初始位置（每次进入场景时记录）")]
    [SerializeField] private Transform spawnPoint;

    private IHealthSystem _health;
    private IRevivable _revivable;
    private CharacterController _controller;

    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;

    void Awake()
    {
        _health = GetComponent<IHealthSystem>();
        _revivable = GetComponent<IRevivable>();
        _controller = GetComponent<CharacterController>();

        // 记录初始坐标为默认复活点（也可在 Inspector 指定 spawnPoint 覆盖）
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
    }

    void OnEnable()
    {
        if (_health != null) _health.OnDied += HandlePlayerDied;

        var gm = GameManager.Instance;
        if (gm != null) gm.RegisterPlayer(this);
    }

    void OnDisable()
    {
        if (_health != null) _health.OnDied -= HandlePlayerDied;

        var gm = GameManager.Instance;
        if (gm != null) gm.UnregisterPlayer(this);
    }

    private void HandlePlayerDied()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.NotifyPlayerDied();
    }

    /// <summary>
    /// 复活流程（由 GameManager.RespawnPlayer 调用）。
    /// 分两层复位，职责清晰：
    ///   数据层 —— 满血 + 解除死亡锁（委托给 IRevivable，位置无关）
    ///   空间层 —— 传送到复活点（本组件负责）
    /// </summary>
    public void RevivePlayer()
    {
        if (_revivable != null) _revivable.Revive();

        Vector3 position = spawnPoint != null ? spawnPoint.position : _spawnPosition;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : _spawnRotation;
        Teleport(position, rotation);
    }

    /// <summary>把玩家从死亡点传送回复活点</summary>
    public void TeleportToSpawn()
    {
        Vector3 position = spawnPoint != null ? spawnPoint.position : _spawnPosition;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : _spawnRotation;
        Teleport(position, rotation);
    }

    private void Teleport(Vector3 position, Quaternion rotation)
    {
        // CharacterController 激活时会接管 transform 的位移，直接赋值会被它覆盖，
        // 因此必须"先禁用 → 传送到位 → 再启用"，这是 Unity 的既定做法。
        if (_controller != null) _controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (_controller != null) _controller.enabled = true;
    }

    // Scene 视图可视化：选中玩家时可看到复活点
    void OnDrawGizmosSelected()
    {
        Vector3 position = spawnPoint != null ? spawnPoint.position : _spawnPosition;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(position, 0.5f);
        Gizmos.DrawLine(position, position + Vector3.up * 2f);
    }
}
