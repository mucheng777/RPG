using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 血条 UI（表现层）。
/// 只订阅 IHealthSystem 的事件，不引用任何具体血量类 → 玩家/敌人/同一 prefab 通用。
///
/// 动画结构（两个值分离，经典做法）：
///   targetFill  —— 逻辑真实血量百分比，事件来了立刻更新
///   frontFill   —— 前景条（红），帧率无关平滑追赶 target，形成"掉血动画"
///   ghostFill   —— 残影条（白，可选），受伤后先停留、延迟再缓慢追赶前景，
///                  强化"掉了多少血"的视觉对比；治疗时立即同步，不反向拖尾
///
/// 初始化安全性：
///   不依赖 Start 执行顺序——首次收到事件时立即对齐三条值，之后才启用插值。
/// 死亡隐藏：
///   优先用 CanvasGroup 调透明度（不影响脚本生命周期），无 CanvasGroup 则隐藏 Image。
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Tooltip("血量数据源（挂 IHealthSystem 的物体）。不填自动从父物体查找。")]
    [SerializeField] private MonoBehaviour target;

    [Header("血条图像")]
    [Tooltip("前景填充条（Filled），平滑跟随当前血量")]
    [SerializeField] private Image fillImage;

    [Tooltip("残影条（可选，放在 fillImage 下层，颜色建议白/黄）。不填则只有平滑掉血。")]
    [SerializeField] private Image ghostImage;

    [Header("动画参数")]
    [Tooltip("前景条追赶速度（越大越快，约 8~12 手感较好）")]
    [SerializeField] private float fillLerpSpeed = 10f;

    [Tooltip("受伤后残影停留多久才开始追赶（秒）")]
    [SerializeField] private float ghostDelay = 0.45f;

    [Tooltip("残影追赶速度（明显慢于前景条）")]
    [SerializeField] private float ghostLerpSpeed = 3f;

    private IHealthSystem _health;
    private CanvasGroup _canvasGroup;

    private float _targetFill = 1f;
    private float _frontFill = 1f;
    private float _ghostFill = 1f;
    private float _ghostTimer;
    private bool _initialized;

    void Awake()
    {
        if (target != null)
            _health = target as IHealthSystem;
        if (_health == null)
            _health = GetComponentInParent<IHealthSystem>();

        _canvasGroup = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (_health == null) return;
        _health.OnHealthChanged += OnHealthChanged;
        _health.OnDied += HideBar;
    }

    void OnDisable()
    {
        if (_health == null) return;
        _health.OnHealthChanged -= OnHealthChanged;
        _health.OnDied -= HideBar;
    }

    /// <summary>事件回调：只更新"目标值"，真正的动画在 Update 里插值</summary>
    private void OnHealthChanged(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        // 首次同步：立即对齐，不播从 0 开始的错误动画（规避 Start 顺序问题）
        if (!_initialized)
        {
            _targetFill = _frontFill = _ghostFill = ratio;
            ApplyVisual();
            _initialized = true;
            return;
        }

        if (ratio < _targetFill)
        {
            // 掉血：前景条立即设定新目标开始平滑下降；残影保持旧值，延迟后追赶
            _ghostTimer = ghostDelay;
        }
        else
        {
            // 治疗/复活：残影不反向拖尾，直接同步到新值
            _ghostFill = ratio;
            _ghostTimer = 0f;
        }

        _targetFill = ratio;
    }

    void Update()
    {
        if (!_initialized) return;

        bool changed = false;

        // 帧率无关的指数阻尼：t = 1 - e^(-k·dt)
        if (!Mathf.Approximately(_frontFill, _targetFill))
        {
            _frontFill = Damp(_frontFill, _targetFill, fillLerpSpeed);
            changed = true;
        }

        if (_ghostTimer > 0f)
        {
            _ghostTimer -= Time.deltaTime;
        }
        else if (_ghostFill > _frontFill + 0.001f)
        {
            _ghostFill = Damp(_ghostFill, _frontFill, ghostLerpSpeed);
            changed = true;
        }

        if (changed) ApplyVisual();
    }

    private static float Damp(float current, float target, float speed)
        => Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.deltaTime));

    private void ApplyVisual()
    {
        if (fillImage != null) fillImage.fillAmount = _frontFill;
        if (ghostImage != null) ghostImage.fillAmount = _ghostFill;
    }

    private void HideBar()
    {
        // 用 CanvasGroup 隐藏而不是 SetActive：避免组件失活导致 Update/重订阅异常
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }
        else
        {
            if (fillImage != null) fillImage.enabled = false;
            if (ghostImage != null) ghostImage.enabled = false;
        }
    }
}
