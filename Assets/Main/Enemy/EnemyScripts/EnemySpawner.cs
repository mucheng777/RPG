using UnityEngine;

/// <summary>
/// 最小刷怪演示器：通过 PoolManager 生成敌人，用来验证"取货 → 死亡归还 → 再取货"的复用链路。
///
/// 只是演示与调试用，池系统本身不依赖本组件 —— 不需要时整份删掉即可，
/// 以后正式的刷怪逻辑（关卡波次、刷怪点）另写组件调用同一个 PoolManager。
/// </summary>
[AddComponentMenu("RPG/Enemy/Enemy Spawner")]
public class EnemySpawner : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("敌人的源预制体（必须拖 Project 里的预制体，不能拖场景里的实例）")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("每波生成数量")]
    [SerializeField] private int countPerWave = 3;

    [Tooltip("出生点在半径内的随机散布（米）")]
    [SerializeField] private float spawnRadius = 4f;

    [Tooltip("波次间隔（秒）；<= 0 表示只在开始时刷一波")]
    [SerializeField] private float waveInterval = 0f;

    void Start()
    {
        SpawnWave();
        if (waveInterval > 0f)
            InvokeRepeating(nameof(SpawnWave), waveInterval, waveInterval);
    }

    /// <summary>生成一波敌人（也可由 UI、关卡脚本、触发器调用）</summary>
    public void SpawnWave()
    {
        if (enemyPrefab == null || PoolManager.Instance == null) return;

        for (int i = 0; i < countPerWave; i++)
        {
            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = 0f; // 只在水平面散布，避免生成在空中
            PoolManager.Instance.Spawn(enemyPrefab, transform.position + offset, Quaternion.identity);
        }
    }
}
