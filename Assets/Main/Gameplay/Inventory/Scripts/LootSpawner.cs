using UnityEngine;

/// <summary>
/// 掉落物生成器（静态服务）—— 把"掉落表结算结果"翻译成"地面实体"。
///
/// 职责边界：
///   - LootTable 决定掉什么；
///   - 本类负责散落位置、贴地、实例化/回池、绑定 ItemPickup；
///   - 调用方只给一个死亡点坐标，不需要知道这些细节。
///
/// 生成策略：
///   - 配置了 LootTable.dropPrefab 且场景有 PoolManager → 走对象池（推荐）；
///   - 只配了 dropPrefab → 直接 Instantiate；
///   - 都没配 → 运行时创建空物体 + ItemPickup，由它自己生成占位球体（零素材也能跑）。
/// </summary>
public static class LootSpawner
{
    /// <summary>按掉落表在死亡点附近生成全部掉落物。</summary>
    /// <param name="ignoreRoot">贴地射线要忽略的物体（死亡敌人自身），避免物品停在尸体头顶</param>
    public static void Spawn(LootTable table, Vector3 deathPosition, Transform ignoreRoot = null)
    {
        if (table == null) return;

        var drops = table.Roll();
        for (int i = 0; i < drops.Count; i++)
        {
            LootDrop drop = drops[i];
            if (drop.item == null || drop.count <= 0) continue;

            // 在水平面上随机散开，避免多个掉落物完全重叠
            Vector2 offset = Random.insideUnitCircle * table.scatterRadius;
            Vector3 point = deathPosition + new Vector3(offset.x, 0f, offset.y);

            SpawnOne(table, drop.item, drop.count, SnapToGround(point, table, ignoreRoot));
        }
    }

    /// <summary>
    /// 生成单个掉落物（拾取/测试/任务奖励都可复用）。
    /// 返回生成的 ItemPickup，失败时返回 null。
    /// </summary>
    public static ItemPickup SpawnOne(LootTable table, ItemDefinition item, int count, Vector3 position)
    {
        if (item == null || count <= 0) return null;

        GameObject prefab = table != null ? table.dropPrefab : null;
        GameObject instance;

        if (prefab != null)
        {
            instance = PoolManager.Instance != null
                ? PoolManager.Instance.Spawn(prefab, position, Quaternion.identity)
                : Object.Instantiate(prefab, position, Quaternion.identity);

            if (instance == null) return null;
        }
        else
        {
            instance = new GameObject("Drop");
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
        }

        instance.transform.position = position; // 池对象复用后确保位置正确

        ItemPickup pickup = instance.GetComponent<ItemPickup>();
        if (pickup == null) pickup = instance.AddComponent<ItemPickup>();

        pickup.Setup(item, count);
        return pickup;
    }

    /// <summary>
    /// 从上方往下打射线把掉落物贴到地面，打不到就保持原高度。
    /// 会跳过 ignoreRoot 自身（死亡敌人）的碰撞体：敌人死亡时碰撞体往往还在，
    /// 直接打射线会命中尸体胶囊，导致物品停在半空。
    /// </summary>
    private static Vector3 SnapToGround(Vector3 point, LootTable table, Transform ignoreRoot)
    {
        Vector3 origin = point + Vector3.up * 2f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 6f, table.groundMask, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        bool found = false;
        Vector3 result = point;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot)) continue;
            if (hits[i].distance >= nearest) continue;

            nearest = hits[i].distance;
            result = hits[i].point + Vector3.up * 0.05f;
            found = true;
        }

        return found ? result : point;
    }
}
