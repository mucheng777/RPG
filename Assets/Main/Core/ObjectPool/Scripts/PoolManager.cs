using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局对象池注册表 —— 按"预制体"分池，是所有生成/回收的统一入口。
///
/// 使用方只认两个方法：
///   Spawn(prefab, pos, rot) —— 从池里取一个敌人/子弹/任何预制体
///   Despawn(instance)      —— 把实例还回去（不是池里出来的对象会自动退化为销毁）
/// 因此以后新增池化类型（子弹、伤害数字、可破坏物）本类零改动 —— 只认 GameObject，不认业务。
///
/// 单例约定沿用项目里 GameManager 的写法：静态 Instance + 惰性查找兜底，
/// 避免"脚本执行顺序导致 Instance 还没赋值"的隐性 bug。
/// </summary>
[AddComponentMenu("RPG/Common/Pool Manager")]
public class PoolManager : MonoBehaviour
{
    private static PoolManager _instance;

    public static PoolManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<PoolManager>();
            return _instance;
        }
    }

    [Header("配置")]
    [Tooltip("每个池首次建池时预先实例化的数量（0 = 不预热，第一次取货时才 Instantiate）")]
    [SerializeField] private int prewarmCount = 0;

    /// <summary>预制体 → 对应的池</summary>
    private readonly Dictionary<GameObject, ObjectPool<Transform>> _pools
        = new Dictionary<GameObject, ObjectPool<Transform>>();

    /// <summary>实例 → 它来自哪个预制体（Despawn 时靠它找到该还哪个池）</summary>
    private readonly Dictionary<GameObject, GameObject> _sourcePrefab
        = new Dictionary<GameObject, GameObject>();

    void Awake()
    {
        // 单例守卫：与 GameManager 同一套约定，重复实例直接销毁
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[PoolManager] 场景中存在多个 PoolManager，已销毁重复实例。", this);
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>从池中取一个实例并放到指定位置（该预制体还没建池时会自动新建）</summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[PoolManager] Spawn 的 prefab 为空，已忽略。", this);
            return null;
        }

        if (!_pools.TryGetValue(prefab, out var pool))
        {
            // 每个预制体一个池根节点，回收实例都挂这里，Hierarchy 一眼能看清有几个池
            var root = new GameObject($"Pool_{prefab.name}").transform;
            root.SetParent(transform, false);
            pool = new ObjectPool<Transform>(prefab.transform, root, prewarmCount);
            _pools[prefab] = pool;
        }

        Transform instance = pool.Get(position, rotation);
        _sourcePrefab[instance.gameObject] = prefab;
        return instance.gameObject;
    }

    /// <summary>把实例归还进池等待复用；若不是池生成的（场景手摆的）则退化为销毁</summary>
    public void Despawn(GameObject instance)
    {
        if (instance == null) return;

        if (_sourcePrefab.TryGetValue(instance, out var prefab) && _pools.TryGetValue(prefab, out var pool))
        {
            _sourcePrefab.Remove(instance);
            pool.Release(instance.transform);
            return;
        }

        // 兜底：手摆对象还给池会污染池的复用逻辑（它没有对应的池），直接销毁更安全
        Destroy(instance);
    }
}
