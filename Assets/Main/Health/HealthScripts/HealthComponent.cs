using System;
using UnityEngine;

/// <summary>
/// 通用血量组件，实现 IHealthProvider + IDamageable。
/// 玩家和敌人都可以直接挂这个组件，也可继承后加专属逻辑
/// （见 PlayerHealth / EnemyHealth）。
///
/// 设计要点：
///   1. 事件驱动：血量变化时主动通知 UI，而非 UI 每帧轮询（性能+解耦）
///   2. 死亡锁：死后 TakeDamage 直接 return，防止重复触发 OnDied
///   3. virtual 方法：子类可 override 加专属逻辑（无敌帧、护盾、受击硬直等）
/// </summary>
public class HealthComponent : MonoBehaviour, IHealthProvider, IDamageable
{
    [SerializeField] protected HealthProfile profile;

    protected float _currentHealth;
    protected bool _isAlive = true;

    // ===== IHealthProvider =====
    public float MaxHealth => profile != null ? profile.maxHealth : 100f;
    public float CurrentHealth => _currentHealth;
    public bool IsAlive => _isAlive;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    // ===== IDamageable =====
    public event Action<DamageInfo> OnDamaged;

    void Awake()
    {
        _currentHealth = MaxHealth;
    }

    void Start()
    {
        // 初始化时通知 UI 显示满血
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    /// <summary>
    /// 承受伤害。子类可通过 override 加入无敌帧、护盾减伤等前置逻辑。
    /// </summary>
    public virtual void TakeDamage(DamageInfo info)
    {
        if (!IsAlive) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - info.Amount);

        OnDamaged?.Invoke(info);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

        if (_currentHealth <= 0f && _isAlive)
        {
            _isAlive = false;
            OnDied?.Invoke();
            HandleDeath();
        }
    }

    public virtual void Heal(float amount)
    {
        if (!IsAlive) return;
        _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    /// <summary>
    /// 死亡后的默认处理：延迟销毁。
    /// 子类可 override 改为播放死亡动画、掉落物品等。
    /// </summary>
    protected virtual void HandleDeath()
    {
        if (profile != null && profile.destroyDelay > 0f)
            Destroy(gameObject, profile.destroyDelay);
    }
}
