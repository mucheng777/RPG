using UnityEngine;

/// <summary>
/// 敌人配置（ScriptableObject）。
/// 符合项目惯例：配置与逻辑分离。
/// 以后扩展 AI 时，在此加追击范围、攻击间隔、巡逻路径等参数。
/// </summary>
[CreateAssetMenu(fileName = "EnemyProfile", menuName = "RPG/Enemy Profile")]
public class EnemyProfile : ScriptableObject
{
    [Header("基础属性")]
    [Tooltip("最大血量")]
    public float maxHealth = 50f;

    [Tooltip("移动速度")]
    public float moveSpeed = 2f;

    [Header("攻击")]
    [Tooltip("攻击伤害")]
    public float attackDamage = 10f;

    [Tooltip("攻击间隔（秒）")]
    public float attackInterval = 2f;

    [Tooltip("攻击距离")]
    public float attackRange = 1.5f;

    [Header("AI 感知（以后扩展用）")]
    [Tooltip("追击触发距离")]
    public float chaseRange = 8f;

    [Tooltip("丢失目标距离")]
    public float loseTargetRange = 12f;

    [Header("死亡")]
    [Tooltip("死亡后延迟销毁时间")]
    public float destroyDelay = 2f;
}
