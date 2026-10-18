/// <summary>
/// 可复活契约（依赖倒置：生命周期系统只依赖此抽象，不依赖 PlayerHealth 具体类）。
///
/// 与 IDamageable / IHealthSystem 的分工：
///   IHealthSystem —— 读血量 + 治疗（UI/技能系统使用）
///   IDamageable   —— 受伤入口（攻击系统使用）
///   IRevivable    —— 从死亡状态恢复到可战斗状态（游戏流程系统使用）
///
/// 实现约定：
///   1. Revive() 必须把数据状态复位为"满血 + 存活"（解除死亡锁），
///      并广播一次血量变化事件，让血条等 UI 重新同步。
///   2. 只负责"数据层"复位；位置/表现的复位由调用方（PlayerLifecycleBridge）负责，
///      保持单一职责。
///   3. 必须幂等：对存活对象重复调用不产生副作用。
/// </summary>
public interface IRevivable
{
    /// <summary>复活：恢复到满血、存活、并广播血量事件</summary>
    void Revive();
}
