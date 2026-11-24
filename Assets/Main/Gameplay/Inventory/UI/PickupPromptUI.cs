using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 拾取提示 UI —— 玩家靠近地面掉落物时，屏幕下方显示"最近的那件物品"的信息。
///
/// 交互：
///   - 只做信息展示，不解锁光标、不接管鼠标，视角和操作全程不受影响；
///   - 按 E 拾取当前显示的物品。
///
/// 解耦说明：
///   - 只认识 ItemPickup（数据 + 拾取入口）和 Inventory（数据），不关心物品类型；
///   - 通过 ItemPickup.ActivePickups 做距离筛选，不依赖物理触发器，避免每帧 OverlapSphere；
///   - 背包打开、暂停、死亡、主菜单时自动隐藏，不残留提示。
/// </summary>
[DefaultExecutionOrder(60)]
[AddComponentMenu("RPG/Inventory/Pickup Prompt UI")]
public class PickupPromptUI : MonoBehaviour
{
    [Header("拾取")]
    [Tooltip("进入这个距离内显示最近掉落物的信息")]
    [Min(0.5f)]
    [SerializeField] private float pickupRange = 3f;

    [Tooltip("快捷拾取按键")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E;

    [Header("界面引用（必填：把 Hierarchy 里的控件拖进来）")]
    [Tooltip("提示条的根节点（一个 Panel），靠近时显示、离开时隐藏")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("提示文字")]
    [SerializeField] private TMP_Text label;

    private Inventory _inventory;
    private InventoryUI _inventoryUI;
    private PlayerController _player;
    private ItemPickup _target;
    private int _lastCount = -1;
    private bool _visible;

    void Awake()
    {
        if (panelRoot == null || label == null)
        {
            Debug.LogError("[PickupPromptUI] 未指定提示条根节点或文字，拾取提示不会显示。", this);
            enabled = false;
            return;
        }

        panelRoot.SetActive(false);
    }

    void Start()
    {
        _inventory = FindFirstObjectByType<Inventory>();
        _inventoryUI = InventoryUI.Instance;
        _player = FindFirstObjectByType<PlayerController>();
    }

    void OnDisable()
    {
        SetVisible(false);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && !gm.IsPlaying) { SetVisible(false); return; }

        if (_inventoryUI == null) _inventoryUI = InventoryUI.Instance;
        if (_inventoryUI != null && _inventoryUI.IsOpen) { SetVisible(false); return; }

        if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
        if (_player == null) _player = FindFirstObjectByType<PlayerController>();

        // 目标被外部销毁/回收（存在时间到期、被其他途径拾取）时，Unity 的 == null 为 true，
        // 这里必须显式清掉，否则信息条会一直停留在屏幕上
        if (_target == null && _visible)
        {
            _lastCount = -1;
            SetVisible(false);
        }

        ItemPickup nearest = FindNearest();
        if (nearest != _target)
        {
            _target = nearest;
            SetVisible(_target != null);
            if (_target != null) UpdateLabel();
        }
        else if (_target != null && _target.Count != _lastCount)
        {
            UpdateLabel();
        }

        if (_target != null && Input.GetKeyDown(pickupKey)) Collect();
    }

    // ==================== 查找 / 显示 ====================

    private ItemPickup FindNearest()
    {
        // 距离必须以玩家为基准：Inventory 挂在一个常驻系统物体上（坐标通常在原点），用它测距永远不会命中
        Vector3 origin = _player != null
            ? _player.transform.position
            : (_inventory != null ? _inventory.transform.position : transform.position);
        float bestSqr = pickupRange * pickupRange;
        ItemPickup best = null;

        // 倒序遍历：允许遍历过程中安全移除已销毁对象
        var pickups = ItemPickup.ActivePickups;
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            ItemPickup pickup = pickups[i];
            if (pickup == null || pickup.Item == null) continue;

            float sqr = (pickup.transform.position - origin).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = pickup;
        }

        return best;
    }

    private void UpdateLabel()
    {
        if (label == null || _target == null) return;

        _lastCount = _target.Count;
        label.text = $"[{pickupKey}] 拾取 {_target.Item.displayName} ×{_target.Count} · {_target.Item.itemType.GetDisplayName()}";
    }

    private void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;

        // 只切换显示，不碰光标/相机输入：靠近战利品时依然保持锁定视角
        if (panelRoot != null) panelRoot.SetActive(visible);
    }

    private void Collect()
    {
        if (_target == null) return;
        if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
        if (_inventory == null) return;

        if (_target.TryCollect(_inventory))
        {
            _target = null;
            _lastCount = -1;
            SetVisible(false);
        }
        else if (label != null)
        {
            label.text = "背包已满";
        }
    }
}
