/// <summary>
/// 池化对象生命周期接口（预留接入点）。
///
/// 池系统（ObjectPool / PoolManager）对取出的对象调用：
///   GetComponentsInChildren&lt;IPoolable&gt;() → 逐个 OnSpawned()（复位状态）
/// 回收前调用：
///   逐个 OnDespawned()（退订事件、清理运行时引用）
///
/// 实现约定：
///   1. OnSpawned 必须把对象恢复到"刚 Instantiate 出来"的等价状态
///      （血量、死亡锁、AI 状态机、计时器等——因为 Awake/Start 只在首次激活时执行）。
///   2. OnDespawned 负责清理"运行期产生的外部引用"，防止复活后重复触发。
///   3. 两方法必须幂等：重复调用不产生副作用（防御池系统的双重回收/重复取出）。
/// </summary>
public interface IPoolable
{
    /// <summary>从池中取出、放入场景时调用（等价于"第二次 Awake"）</summary>
    void OnSpawned();

    /// <summary>回收进池之前调用</summary>
    void OnDespawned();
}
