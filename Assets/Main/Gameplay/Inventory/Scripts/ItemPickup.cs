using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地面掉落物实体（挂在掉落物根物体上）。
///
/// 职责：
///   1. 保存"是什么物品、多少个"；
///   2. 生成/刷新占位视觉（没有美术素材时用纯色球体，后面把 worldPrefab 拖进 ItemDefinition 即可替换）；
///   3. 提供统一的拾取入口 TryCollect(Inventory)，自己不认识玩家、UI、输入；
///   4. 维护静态注册表，供拾取提示 UI 找"离玩家最近的那一个"，避免每帧 OverlapSphere。
///
/// 扩展点：
///   - 拾取特效/音效：在 Despawn 前调用；
///   - 自动拾取：外部每帧对 ActivePickups 调 TryCollect 即可；
///   - 池化：LootTable.dropPrefab 指向挂好本组件的预制体时，LootSpawner 会自动走 PoolManager。
/// </summary>
[AddComponentMenu("RPG/Inventory/Item Pickup")]
public class ItemPickup : MonoBehaviour
{
    /// <summary>场景中所有激活的掉落物（拾取提示 UI 遍历它找最近目标）</summary>
    public static readonly List<ItemPickup> ActivePickups = new List<ItemPickup>();

    [Header("内容")]
    [SerializeField] private ItemDefinition item;
    [Min(1)]
    [SerializeField] private int count = 1;

    [Header("表现（占位）")]
    [Tooltip("上下浮动高度")]
    [SerializeField] private float bobHeight = 0.12f;
    [Tooltip("自转速度（度/秒）")]
    [SerializeField] private float spinSpeed = 70f;
    [Tooltip("存在时间（秒），0 = 永不消失")]
    [SerializeField] private float lifetime = 180f;

    private Transform _visual;
    private Vector3 _visualBasePosition;
    private float _lifeTimer;
    private bool _visualIsPrefab;

    // 所有占位物体共用一份材质，颜色通过 MaterialPropertyBlock 逐实例设置，避免材质泄漏
    private static Material _placeholderMaterial;

    public ItemDefinition Item => item;
    public int Count => count;

    void Awake()
    {
        EnsureVisual();
        _lifeTimer = lifetime;
    }

    void OnEnable()
    {
        if (!ActivePickups.Contains(this)) ActivePickups.Add(this);
    }

    void OnDisable()
    {
        ActivePickups.Remove(this);
    }

    void Update()
    {
        // 浮动 + 自转，让地面物品在场景里更容易被看到（时间冻结时一起停，符合暂停语义）
        if (_visual != null)
        {
            float bob = Mathf.Sin(Time.time * 2f) * bobHeight;
            _visual.localPosition = _visualBasePosition + Vector3.up * bob;
            _visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        if (lifetime > 0f)
        {
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f) Despawn();
        }
    }

    /// <summary>
    /// 由生成方调用：设置内容并刷新视觉。
    /// 对象池复用时会再次调用，因此这里必须幂等、且不依赖 Awake/Start。
    /// </summary>
    public void Setup(ItemDefinition definition, int amount)
    {
        item = definition;
        count = Mathf.Max(1, amount);
        _lifeTimer = lifetime;

        name = definition != null ? $"Drop_{definition.itemId}" : "Drop_Unknown";
        EnsureVisual();
        ApplyVisualColor();
    }

    /// <summary>
    /// 尝试把物品放进背包。
    /// 返回 true 表示至少放进了一部分；返回 false 表示背包已满（物品保留在地面）。
    /// </summary>
    public bool TryCollect(Inventory inventory)
    {
        if (inventory == null || item == null) return false;

        int added = inventory.TryAdd(item, count);
        if (added <= 0) return false;

        count -= added;
        if (count > 0) return true; // 只放进一部分，剩余继续留在地上

        Despawn();
        return true;
    }

    // ==================== 视觉 ====================

    /// <summary>创建占位视觉：优先用 ItemDefinition.worldPrefab，否则程序生成纯色球体。</summary>
    private void EnsureVisual()
    {
        bool wantPrefab = item != null && item.worldPrefab != null;

        // 已经是对应类型的视觉就直接复用；Awake 先于 Setup 执行时会先建占位球，
        // 若随后 Setup 提供了 worldPrefab，这里要把占位视觉换掉
        if (_visual != null && wantPrefab == _visualIsPrefab) return;

        if (_visual != null)
        {
            Destroy(_visual.gameObject);
            _visual = null;
        }

        _visualIsPrefab = wantPrefab;

        if (wantPrefab)
        {
            GameObject instance = Instantiate(item.worldPrefab, transform);
            instance.transform.localPosition = Vector3.zero;
            _visual = instance.transform;
        }
        else
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "PlaceholderVisual";

            // 占位球只做视觉，碰撞判定由根物体的触发球负责，避免两套碰撞体互相干扰
            Collider primitiveCollider = sphere.GetComponent<Collider>();
            if (primitiveCollider != null) Destroy(primitiveCollider);

            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = Vector3.up * 0.35f;
            sphere.transform.localScale = Vector3.one * 0.45f;

            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = GetPlaceholderMaterial();
                if (material != null) renderer.sharedMaterial = material;
            }

            _visual = sphere.transform;
        }

        _visualBasePosition = _visual.localPosition;

        // 根物体上的 Trigger 球：提供"独立碰撞判定"，也是后续物理拾取/触发检测的接入点
        var trigger = GetComponent<SphereCollider>();
        if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.35f, 0f);
        trigger.radius = 0.5f;
    }

    private void ApplyVisualColor()
    {
        // 只有程序生成的占位球才染色；正式掉落预制体保留自身材质
        if (_visual == null || _visualIsPrefab) return;

        Color color = item != null ? item.worldColor : Color.white;
        var renderers = _visual.GetComponentsInChildren<Renderer>(true);
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", color); // URP Lit / Unlit
            block.SetColor("_Color", color);     // 内置管线兜底
            renderers[i].SetPropertyBlock(block);
        }
    }

    private static Material GetPlaceholderMaterial()
    {
        if (_placeholderMaterial != null) return _placeholderMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        _placeholderMaterial = new Material(shader) { name = "ItemPlaceholder(Shared)" };
        return _placeholderMaterial;
    }

    private void Despawn()
    {
        // 池内对象回池，场景手摆或程序创建的走销毁（PoolManager 内部已做兜底）
        if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
        else Destroy(gameObject);
    }
}
