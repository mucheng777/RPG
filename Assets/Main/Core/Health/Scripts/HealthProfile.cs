using UnityEngine;

/// <summary>
/// 血量配置（ScriptableObject）。
/// 符合项目惯例：配置与逻辑分离，策划可在 Inspector 调参。
/// 玩家和敌人各自创建一个 Profile 实例，复用同一份脚本。
/// </summary>
[CreateAssetMenu(fileName = "HealthProfile", menuName = "RPG/Health Profile")]
public class HealthProfile : ScriptableObject
{
    [Header("基础血量")]
    [Tooltip("最大血量")]
    public float maxHealth = 100f;

    [Header("死亡处理")]
    [Tooltip("死亡后延迟销毁的时间（秒），0=不自动销毁")]
    public float destroyDelay = 2f;

    [Header("UI")]
    [Tooltip("是否显示血条")]
    public bool showHealthBar = true;

    [Tooltip("血条是否在世界空间（头顶）显示，false 则需手动在屏幕空间UI挂")]
    public bool worldSpaceUI = true;
}
