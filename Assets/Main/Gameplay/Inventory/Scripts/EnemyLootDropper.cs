using UnityEngine;

/// <summary>
/// 敌人掉落组件 —— 订阅敌人生命系统的死亡事件，按掉落表生成地面物品。
///
/// 为什么单独做成组件（而不是改 EnemyHealth）：
///   - EnemyHealth 已经承担"死亡 → 延迟回池"，不应再被塞进掉落/任务/成就等业务；
///   - 本组件只依赖 IHealthSystem（接口），对敌人类型零感知，
///     以后 Boss、可破坏物、宝箱只要挂上它并配一张掉落表就能掉落；
///   - 不改动既有脚本，符合"只做增量接入"的要求。
///
/// 对象池注意：
///   - OnEnable/OnDisable 成对订阅/退订，池对象复用时不会重复订阅；
///   - 每次复用重置 _hasDropped，保证一命只掉一次。
/// </summary>
[AddComponentMenu("RPG/Inventory/Enemy Loot Dropper")]
[RequireComponent(typeof(HealthComponent))]
public class EnemyLootDropper : MonoBehaviour
{
    [Header("掉落配置")]
    [Tooltip("掉落表：留空则该敌人不掉落")]
    [SerializeField] private LootTable lootTable;

    [Tooltip("死亡后延迟多久掉落（秒）。接入死亡动画后可设置为动画时长")]
    [Min(0f)]
    [SerializeField] private float dropDelay = 0f;

    [Tooltip("掉落点相对敌人根节点的高度偏移（避免物品卡在地里）")]
    [SerializeField] private float heightOffset = 0.6f;

    [Tooltip("是否在控制台打印掉落信息（调试用）")]
    [SerializeField] private bool debugLog = true;

    private HealthComponent _health;
    private bool _hasDropped;

    void Awake()
    {
        _health = GetComponent<HealthComponent>();
    }

    void OnEnable()
    {
        _hasDropped = false;
        if (_health != null)
        {
            _health.OnDied -= HandleDied; // 防重复订阅（池对象多次启用）
            _health.OnDied += HandleDied;
        }
    }

    void OnDisable()
    {
        if (_health != null) _health.OnDied -= HandleDied;
        CancelInvoke(nameof(Drop)); // 延迟期间被回收时，取消待执行的掉落，避免池对象复活后乱掉
    }

    private void HandleDied()
    {
        if (_hasDropped) return;
        _hasDropped = true;

        if (dropDelay > 0f) Invoke(nameof(Drop), dropDelay);
        else Drop();
    }

    private void Drop()
    {
        if (lootTable == null) return;

        Vector3 point = transform.position + Vector3.up * heightOffset;
        LootSpawner.Spawn(lootTable, point);

        if (debugLog) Debug.Log($"[Loot] {name} 触发掉落表 {lootTable.name}", this);
    }
}
