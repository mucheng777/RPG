using UnityEngine;

/// <summary>
/// 敌人控制器（木桩版）。
///
/// 当前阶段：只做"能被打的死物"，伤害管线跑通用。
/// 以后扩展 AI 时，在此加状态机：
///   - EnemyState（基类）
///   - IdleState / PatrolState / ChaseState / AttackState / HurtState / DeadState
/// 感知系统接口化（ITargetProvider），不硬编码玩家引用。
///
/// 注意：不依赖 PlayerController，通过 ITargetProvider 获取目标。
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Tooltip("敌人配置")]
    [SerializeField] private EnemyProfile profile;

    // ===== 以后扩展用（当前留空）=====
    // private EnemyBrain _brain;
    // private ITargetProvider _targetProvider;

    void Awake()
    {
        // 把 EnemyProfile 的血量同步到 EnemyHealth
        var health = GetComponent<EnemyHealth>();
        if (health != null && profile != null)
        {
            // HealthComponent 读的是 HealthProfile，这里做桥接：
            // 如果敌人用 EnemyProfile 而非 HealthProfile，需要手动设置血量
            // 简单做法：让 EnemyHealth override Awake 读 EnemyProfile
        }
    }

    // 以后 AI 的 Update 入口
    // void Update() { _brain?.Tick(); }
}
