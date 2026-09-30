using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 血条UI组件。
/// 只依赖 IHealthProvider 接口，不依赖 PlayerHealth/EnemyHealth →
/// 同一个 prefab 既能挂玩家头顶，也能挂敌人头顶。
///
/// 使用方式：
///   1. 在角色下建 World Space Canvas → Image(Filled) → 挂本脚本
///   2. 把 IHealthProvider 所在 GameObject 拖到 target 字段
///      （不拖也行，Awake 会自动 GetComponentInParent 查找）
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Tooltip("血量数据源。不填则自动从父物体查找 IHealthProvider。")]
    [SerializeField] private MonoBehaviour target;

    [Tooltip("血条填充 Image（Filled 类型）")]
    [SerializeField] private Image fillImage;

    private IHealthProvider _health;

    void Awake()
    {
        if (target != null)
            _health = target as IHealthProvider;

        if (_health == null)
            _health = GetComponentInParent<IHealthProvider>();
    }

    void OnEnable()
    {
        if (_health != null)
        {
            _health.OnHealthChanged += UpdateBar;
            _health.OnDied += HideBar;
        }
    }

    void OnDisable()
    {
        if (_health != null)
        {
            _health.OnHealthChanged -= UpdateBar;
            _health.OnDied -= HideBar;
        }
    }

    void Start()
    {
        if (_health != null)
            UpdateBar(_health.CurrentHealth, _health.MaxHealth);
    }

    private void UpdateBar(float current, float max)
    {
        if (fillImage != null)
            fillImage.fillAmount = max > 0f ? current / max : 0f;
    }

    private void HideBar()
    {
        gameObject.SetActive(false);
    }
}
