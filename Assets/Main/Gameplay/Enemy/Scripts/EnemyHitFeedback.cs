using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人受击反馈：命中闪白 + 小击退。
///
/// 挂在敌人根物体（与 EnemyHealth 同级）即可，监听 OnDamaged 自动触发，
/// 不改 EnemyBrain / EnemyAnimator 的任何逻辑。
///
/// 两个实现要点：
///   1. 闪白：用 MaterialPropertyBlock 改 URP Lit 的 _BaseColor，不污染共享材质；
///      恢复时 SetPropertyBlock(null) 直接回到材质原色。
///   2. 击退：必须用 NavMeshAgent.Move 而不是 transform.position。
///      Agent 每帧会把 transform 同步回自己的位置，手改 transform 会被打回；
///      且 Move 的偏移是「自身局部空间」，要先用 InverseTransformDirection 转一下。
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[AddComponentMenu("RPG/Enemy/Enemy Hit Feedback")]
public class EnemyHitFeedback : MonoBehaviour
{
    [Header("闪白")]
    [Tooltip("闪白持续时间（秒，真实时间，不受命中顿帧影响）")]
    [Min(0.01f)]
    [SerializeField] private float flashDuration = 0.08f;

    [Tooltip("闪白色（乘到 _BaseColor 上，大于 1 会更亮更白）")]
    [SerializeField] private Color flashColor = new Color(3f, 3f, 3f, 1f);

    [Header("击退")]
    [Tooltip("击退距离（米）")]
    [Min(0f)]
    [SerializeField] private float knockbackDistance = 0.35f;

    [Tooltip("击退持续时间（秒）")]
    [Min(0.01f)]
    [SerializeField] private float knockbackDuration = 0.12f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private EnemyHealth _health;
    private NavMeshAgent _agent;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Coroutine _flashRoutine;
    private Coroutine _knockbackRoutine;

    void Awake()
    {
        _health = GetComponent<EnemyHealth>();
        _agent = GetComponent<NavMeshAgent>();
        _renderers = GetComponentsInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (_health != null) _health.OnDamaged += OnDamaged;
    }

    void OnDisable()
    {
        if (_health != null) _health.OnDamaged -= OnDamaged;

        // 组件禁用/对象回收时清掉闪白覆盖，避免下次出场还是白的
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
        if (_knockbackRoutine != null)
        {
            StopCoroutine(_knockbackRoutine);
            _knockbackRoutine = null;
        }
        ClearFlash();
    }

    private void OnDamaged(DamageInfo info)
    {
        Flash();
        Knockback(info.Source);
    }

    // ==================== 闪白 ====================

    private void Flash()
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        _mpb.Clear();
        _mpb.SetColor(BaseColorId, flashColor);
        for (int i = 0; i < _renderers.Length; i++)
            _renderers[i].SetPropertyBlock(_mpb);

        // 必须用真实时间：命中顿帧期间 timeScale 只有 0.05，
        // 用 WaitForSeconds 会把 0.08 秒的闪白拖成 1.6 秒
        yield return new WaitForSecondsRealtime(flashDuration);

        ClearFlash();
        _flashRoutine = null;
    }

    private void ClearFlash()
    {
        for (int i = 0; i < _renderers.Length; i++)
            _renderers[i].SetPropertyBlock(null);
    }

    // ==================== 击退 ====================

    private void Knockback(Transform source)
    {
        if (source == null || _agent == null || !_agent.isOnNavMesh) return;
        if (knockbackDistance <= 0f) return;

        Vector3 dir = transform.position - source.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        if (_knockbackRoutine != null) StopCoroutine(_knockbackRoutine);
        _knockbackRoutine = StartCoroutine(KnockbackRoutine(dir));
    }

    private IEnumerator KnockbackRoutine(Vector3 dir)
    {
        float speed = knockbackDistance / knockbackDuration;
        float elapsed = 0f;
        while (elapsed < knockbackDuration)
        {
            // 用未缩放时间：顿帧时击退也不会被拖慢
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            // Move 的偏移是局部空间，先转成局部方向再乘位移量
            Vector3 localDir = transform.InverseTransformDirection(dir);
            _agent.Move(localDir * (speed * dt));
            yield return null;
        }
        _knockbackRoutine = null;
    }
}
