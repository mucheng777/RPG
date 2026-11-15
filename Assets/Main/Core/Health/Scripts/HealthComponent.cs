using System;
using UnityEngine;

/// <summary>
/// 生命值数据管理组件（玩家/敌人通用基础组件）。
/// 同时实现两个职责分离的接口：
///   IHealthSystem —— 对外只读状态 + 事件 + 治疗（UI/技能系统使用）
///   IDamageable   —— 受伤入口（攻击系统使用）
///
/// 设计要点：
///   1. 单一受伤入口：所有伤害必须走 TakeDamage(DamageInfo)，
///      外部无法直接写 CurrentHealth，保证扣血必经计算/校验/事件链路。
///   2. 事件驱动观察者：血量变化 → OnHealthChanged，UI 自动刷新，逻辑层不引用任何 UI。
///   3. 边界条件：伤害合法性校验、血量钳制 [0, MaxHealth]、死亡锁（只死一次）。
///   4. 开闭原则：ModifyIncomingDamage / HandleDeath 为 protected virtual，
///      子类（PlayerHealth 无敌帧、EnemyHealth 死亡动画、护盾组件）直接扩展，不改本类。
/// </summary>
public class HealthComponent : MonoBehaviour, IHealthSystem, IDamageable
{
    [SerializeField] protected HealthProfile profile;

    [Header("防御")]
    [Tooltip("护甲值：100 点 = 50% 减伤（边际递减公式）。以后可被装备/buff 动态修改。")]
    [Min(0f)]
    [SerializeField] protected float armor;

    protected float _currentHealth;
    protected bool _isAlive = true;

    // ==================== IHealthSystem ====================
    public float MaxHealth => profile != null ? profile.maxHealth : 100f;
    public float CurrentHealth => _currentHealth;
    public bool IsAlive => _isAlive;
    public float Armor => Mathf.Max(0f, armor);

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    // ==================== IDamageable ====================
    public event Action<DamageInfo> OnDamaged;

    void Awake()
    {
        _currentHealth = MaxHealth;
    }

    void Start()
    {
        // 显式初始化协议：所有 Awake 完成后再广播初始值，
        // 此时 UI 的 OnEnable 订阅必然已完成，消除两者 Start 先后顺序的不确定性。
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    // ==================== 受伤管线 ====================

    /// <summary>
    /// 唯一受伤入口。调用链：
    /// 合法性校验 → 死亡锁 → 伤害修正(护甲/护盾) → 钳制扣血 → 事件通知 → 死亡判定
    /// </summary>
    public virtual void TakeDamage(DamageInfo info)
    {
        // 边界条件 1：数值合法性（NaN/负数/0 直接拒绝）
        if (!DamageCalculator.IsValidDamage(info.Amount))
        {
            Debug.LogWarning($"[Health] 非法伤害值 {info.Amount}，已忽略。", this);
            return;
        }

        // 边界条件 2：死亡锁，死后不再结算任何伤害
        if (!_isAlive) return;

        // 伤害修正：默认走护甲公式，True 伤害无视护甲；
        // 护盾/无敌帧/格挡由子类 override 本方法或 ModifyIncomingDamage 实现
        float finalDamage = info.Type == DamageType.True
            ? info.Amount
            : ModifyIncomingDamage(info.Amount);

        // 修正后伤害为 0（如护盾完全抵消）→ 不扣血、不触发受击事件
        if (finalDamage <= 0f) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - finalDamage);

        OnDamaged?.Invoke(info);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

        if (_currentHealth <= 0f)
        {
            _isAlive = false;
            OnDied?.Invoke();
            HandleDeath();
        }
    }

    /// <summary>
    /// 承伤修正钩子（模板方法模式）。
    /// 子类重写示例：
    ///   - 护盾优先：先扣护盾值，溢出部分 base.ModifyIncomingDamage(remain)
    ///   - 临时减伤：return base.ModifyIncomingDamage(raw) * 0.5f
    /// </summary>
    protected virtual float ModifyIncomingDamage(float rawDamage)
        => DamageCalculator.Calculate(rawDamage, Armor);

    // ==================== 治疗 ====================

    public virtual void Heal(float amount)
    {
        if (!_isAlive) return;
        if (!DamageCalculator.IsValidDamage(amount)) return;

        float before = _currentHealth;
        _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);

        // 满血时不产生无意义的事件
        if (!Mathf.Approximately(before, _currentHealth))
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    /// <summary>
    /// 子类专用：手动广播当前血量。
    /// 用于"不走 Awake/Start 初始化"的路径——对象池复活时组件是复用的，
    /// Awake/Start 不会再次执行，需要复活方重置数据后手动通知 UI 同步。
    /// </summary>
    protected void NotifyHealthChanged() => OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

    /// <summary>
    /// 死亡处理钩子。默认按 Profile 延迟销毁；
    /// 子类可 override 为播放死亡动画、掉落、通知 AI 状态机等。
    /// </summary>
    protected virtual void HandleDeath()
    {
        if (profile != null && profile.destroyDelay > 0f)
            Destroy(gameObject, profile.destroyDelay);
    }
}
