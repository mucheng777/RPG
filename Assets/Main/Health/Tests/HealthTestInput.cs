using UnityEngine;

// =====================================================================
// 临时测试模块 —— T 键对自己造成伤害，验证扣血/血条平滑/死亡链路。
//
// 双重生产环境保护：
//   1. 条件编译：整个类包在 #if UNITY_EDITOR || DEVELOPMENT_BUILD 中，
//      正式发布包（非 Development Build）此脚本不参与编译，零运行时开销，
//      等同于"代码不存在"。
//   2. 运行时开关 enableTest：即使在 Editor 中也可一键关闭。
//
// 移除方式：直接删除本文件即可，不引用任何核心模块类型，不影响正式系统。
//
// 使用：挂在 Player 根物体（与 PlayerHealth 同物体），Play 后按 T。
// =====================================================================
#if UNITY_EDITOR || DEVELOPMENT_BUILD

[AddComponentMenu("RPG/Tests/Health Test Input (Editor Only)")]
public class HealthTestInput : MonoBehaviour
{
    [Tooltip("总开关：取消勾选则 T 键无效")]
    [SerializeField] private bool enableTest = true;

    [Tooltip("按一次 T 扣除的伤害")]
    [Min(0f)]
    [SerializeField] private float damagePerPress = 20f;

    [Tooltip("触发按键")]
    [SerializeField] private KeyCode damageKey = KeyCode.T;

    private IDamageable _damageable;
    private IHealthSystem _health;

    void Awake()
    {
        // 面向接口获取，不直接依赖 PlayerHealth
        _damageable = GetComponent<IDamageable>();
        _health = GetComponent<IHealthSystem>();
    }

    void Update()
    {
        if (!enableTest || _damageable == null) return;
        if (!Input.GetKeyDown(damageKey)) return;

        if (_health != null && !_health.IsAlive)
        {
            Debug.Log("[HealthTest] 已死亡，T 键伤害被死亡锁忽略（可在此验证死后不扣血）");
            return;
        }

        // 走正式受伤管线（护甲/事件/死亡判定全部经过），不是直接改血量
        _damageable.TakeDamage(new DamageInfo(damagePerPress, null, DamageType.True));
        Debug.Log($"[HealthTest] T 键造成 {damagePerPress} 点真实伤害（无视护甲）");
    }
}

#endif
