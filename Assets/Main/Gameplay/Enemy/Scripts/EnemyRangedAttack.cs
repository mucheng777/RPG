using UnityEngine;

/// <summary>
/// 远程攻击组件：订阅 EnemyBrain 的出手事件，从发射点吐出一枚炸弹。
///
/// 与 EnemyMeleeAttack 平级可互换——Brain 不感知伤害方式。
///
/// "远程"行为完全由 Profile 数值驱动，不需要专属 AI：
///   attackRange 调大（如 8m）→ Brain 在远处就进 Attack 状态站着吐弹；
///   目标跑出 attackRange×1.2 → 自动回 Chase 追上去，和近战怪同一套状态流转。
///
/// 炸弹走 PoolManager 池化生成，命中/伤害由 EnemyProjectile 自己负责，
/// 本组件只负责"发射"这一下，不跟踪已出膛的弹。
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
[AddComponentMenu("RPG/Enemy/Enemy Ranged Attack")]
public class EnemyRangedAttack : MonoBehaviour
{
    [Header("炸弹")]
    [Tooltip("炸弹预制体（必须挂 EnemyProjectile 脚本）")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("炸弹飞行速度（米/秒）")]
    [SerializeField] private float projectileSpeed = 10f;

    [Header("发射点")]
    [Tooltip("发射点相对自身的位置偏移（嘴部位置，如 (0, 1.2, 0.5)），跟随自身朝向")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.2f, 0.5f);

    [Tooltip("瞄准点相对目标脚下的高度偏移（瞄胸口，不然全打脚）")]
    [SerializeField] private float aimHeight = 1f;

    private EnemyBrain _brain;
    private Transform _pendingTarget;

    void Awake()
    {
        _brain = GetComponent<EnemyBrain>();
    }

    void OnEnable()
    {
        _brain.AttackExecuted += HandleAttackStart;
    }

    void OnDisable()
    {
        _brain.AttackExecuted -= HandleAttackStart;
        _pendingTarget = null;
    }

    /// <summary>出手瞬间：只记录目标，不发射。动画播到吐弹帧才真正开枪。</summary>
    private void HandleAttackStart()
    {
        _pendingTarget = _brain.Target;
    }

    /// <summary>动画事件（Spitter_Spit.anim 的 0.8 秒吐弹瞬间）：此时才生成子弹。</summary>
    public void Shoot()
    {
        Transform target = _pendingTarget;
        _pendingTarget = null;
        if (target == null) return;
        if (projectilePrefab == null || _brain.Profile == null) return;
        if (PoolManager.Instance == null) return;

        // TransformPoint：偏移跟随自身旋转，保证怪物转身后发射点仍在嘴部
        Vector3 spawnPos = transform.TransformPoint(spawnOffset);
        Vector3 aimPos = target.position + Vector3.up * aimHeight;
        Vector3 dir = (aimPos - spawnPos).normalized;

        GameObject go = PoolManager.Instance.Spawn(projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
        if (go == null) return;

        // 用 GetComponentInChildren 兜底：脚本挂在子节点上也能找到，不至于生成一颗"永远浮着的球"
        var projectile = go.GetComponentInChildren<EnemyProjectile>(true);
        if (projectile == null)
        {
            Debug.LogWarning($"[RangedAttack] 炸弹预制体 {projectilePrefab.name} 上没有 EnemyProjectile 脚本！", this);
            return;
        }
        projectile.Launch(dir, projectileSpeed, _brain.Profile.attackDamage, transform);
    }
}
