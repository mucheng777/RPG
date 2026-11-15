using UnityEngine;

/// <summary>
/// 敌人血量组件，继承 HealthComponent，实现 IPoolable 接入对象池。
///
/// 对象池接入点（已接线）：
///   1. 取池复用：池系统取出对象后调用 OnSpawned() → ResetState() 恢复满血/解除死亡锁，
///      并手动广播 OnHealthChanged 同步血条（池复用不走 Awake/Start）。
///   2. 死亡回收：HandleDeath 重写为"延迟归还进池"（PoolManager.Despawn），
///      延迟沿用 HealthProfile.destroyDelay 的语义：2 秒 = 尸体停留 2 秒后回收。
///   3. 状态重置约定集中在 ResetState()，池系统无需了解血量内部字段。
///
/// 扩展点（为以后 AI 预留）：
///   1. OnDamaged 订阅可触发受击硬直（让 EnemyBrain 切到 HurtState）
///   2. OnDied 触发死亡动画 + 通知 EnemyBrain 切到 DeadState（已接线）
///   3. 以后可加弱点倍率（爆头 2x 伤害）→ override TakeDamage
/// </summary>
public class EnemyHealth : HealthComponent, IPoolable
{
    [Header("敌人专属")]
    [Tooltip("受击是否播放硬直动画（需要 EnemyAnimator 配合）")]
    [SerializeField] private bool playHurtAnimation = true;

    /// <summary>
    /// 死亡接管：不再销毁对象，而是延迟归还进池。
    /// 只认 IPoolable + PoolManager，不认识"敌人"以外的任何业务。
    /// 延迟复用父类配置项 destroyDelay，语义从"延迟销毁"变为"延迟回收"，默认值无需调整。
    /// </summary>
    protected override void HandleDeath()
    {
        // TODO(死亡动画): 播放死亡动画，动画结束后再回收（把下面的延迟替换为动画事件回调）。
        if (profile != null && profile.destroyDelay > 0f)
            Invoke(nameof(ReturnToPool), profile.destroyDelay);
        else
            ReturnToPool();
    }

    private void ReturnToPool()
    {
        // 非池化对象（场景手摆的敌人）会被 PoolManager 自动退化为销毁，两种来源共用同一份逻辑
        if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
        else Destroy(gameObject); // 场景没挂 PoolManager 时的兜底，避免尸体永久残留
    }

    // ==================== IPoolable ====================

    /// <summary>池取出时调用：等价于"第二次 Awake"，恢复可战斗状态</summary>
    public void OnSpawned() => ResetState();

    /// <summary>回收进池前调用：当前无运行期外部引用需要清理</summary>
    public void OnDespawned() { }

    /// <summary>
    /// 状态重置协议（池系统/测试均可调用，幂等）。
    /// 重置后必须手动广播血量：复用组件不会再执行 Start 里的初始化广播。
    /// </summary>
    public void ResetState()
    {
        // 取消可能残留的回收 Invoke：若对象被提前回收，复用时那段延迟会误触发新的回收
        CancelInvoke(nameof(ReturnToPool));

        _currentHealth = MaxHealth;
        _isAlive = true;
        NotifyHealthChanged();
    }
}
