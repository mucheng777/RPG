using UnityEngine;

/// <summary>
/// 敌人装配根（组件职责说明，不含运行时逻辑）。
///
/// 敌人由三个独立组件协作，各自单一职责：
///   EnemyBrain  —— AI 状态机（追击/攻击），参数读 EnemyProfile
///   EnemyHealth —— 血量与死亡，参数读 HealthProfile（Inspector 直接拖资产，
///                  与玩家共用同一配置类型；EnemyProfile.healthProfile 只作统一出口供外部系统读取）
///   AttackHitDetection / 玩家攻击 —— 通过 IDamageable 接口命中敌人，与本类无关
///
/// 保留本空壳的原因：场景中敌人已挂本组件，作为"这是一个敌人"的语义标记；
/// 以后的跨组件协调逻辑（如死亡掉落订阅 OnDied）挂在在此处。
/// </summary>
public class EnemyController : MonoBehaviour
{
}
