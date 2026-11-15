using UnityEngine;

/// <summary>
/// 敌人动画桥接组件：把 EnemyBrain 的状态 / EnemyHealth 的事件翻译成 Animator 参数。
///
/// 设计要点：
///   1. 参数名（Speed / IsAttack / IsDead / IsHurt）在这里以 const 定义，
///      AnimatorController 里必须出现同名同类型参数，缺一不可（经验：名字错一字符动画就不动）。
///   2. Speed 从 transform 每帧位移差推算，不依赖 EnemyBrain 暴露速度字段；
///      这样 EnemyBrain 后续切换成 NavMeshAgent / CharacterController 都不用改这里。
///   3. 攻击动画走 Trigger 而不是 Bool：出手时 Trigger 一次，动画播完自动回落，
///      不用担心"一直按着"的状态。EnemyBrain.AttackExecuted 事件每攻击一次触发一次。
///   4. 对象池接入：池复用时 Animator 状态不会自动 Reset（Animator 是组件复用不是重实例化），
///      OnSpawned 里手动归零所有参数，避免刚出场就还保持着"正在死亡"。
///
/// 依赖：
///   - 同 GameObject 上必须有 EnemyBrain（取事件 + IsDead 只读属性）
///   - 同 GameObject 上必须有 EnemyHealth（取 OnDied + OnDamaged）
///   - 子物体上必须有 Animator（SkinnedMeshRenderer 同级或子级）
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
[RequireComponent(typeof(EnemyHealth))]
[DisallowMultipleComponent]
public class EnemyAnimator : MonoBehaviour, IPoolable
{
    [Header("参数名（与 AnimatorController 必须完全一致）")]
    [Tooltip("移动速度，0=Idle，低=Walk，高=Run；BlendTree 用这个")]
    [SerializeField] private string speedParam = "Speed";

    [Tooltip("攻击 Trigger")]
    [SerializeField] private string attackTrigger = "IsAttack";

    [Tooltip("死亡 bool（一死就锁在死亡动画不再动）")]
    [SerializeField] private string deadBool = "IsDead";

    [Tooltip("受击 Trigger（以后接 EnemyHealth.OnDamaged，可选）")]
    [SerializeField] private string hurtTrigger = "IsHurt";

    [Header("移动采样")]
    [Tooltip("速度低于该阈值视为静止（过滤转身/抖动产生的噪声）")]
    [Min(0f)]
    [SerializeField] private float speedDeadzone = 0.05f;

    [Tooltip("目标速度上限，超过则截到此值（让 BlendTree 上边界稳定）")]
    [Min(0.1f)]
    [SerializeField] private float maxSpeed = 5f;

    [Tooltip("Speed 参数平滑时间，避免突然起步/急停造成跳跃")]
    [Min(0f)]
    [SerializeField] private float speedSmoothTime = 0.15f;

    private Animator _animator;
    private EnemyBrain _brain;
    private EnemyHealth _health;

    // 用 string 哈希缓存，避免每帧 StringToHash（Animator 内部也是哈希，这里省一次）
    private int _speedHash;
    private int _attackHash;
    private int _deadHash;
    private int _hurtHash;

    // 速度采样
    private Vector3 _lastPos;
    private float _currentSpeed;
    private bool _wasDead;

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _brain = GetComponent<EnemyBrain>();
        _health = GetComponent<EnemyHealth>();

        _speedHash = Animator.StringToHash(speedParam);
        _attackHash = Animator.StringToHash(attackTrigger);
        _deadHash = Animator.StringToHash(deadBool);
        _hurtHash = Animator.StringToHash(hurtTrigger);

        // 记录初始位置，否则第一帧会把「从原点到现在的位置」当成速度，瞬间冲到上限
        _lastPos = transform.position;
    }

    void OnEnable()
    {
        // 重新启用/池复用时，清掉旧采样，避免第一帧速度跳变
        _lastPos = transform.position;
        _currentSpeed = 0f;
        _velRef = 0f;

        if (_brain != null)
        {
            _brain.StateChanged += OnBrainStateChanged;
            _brain.AttackExecuted += OnAttackExecuted;
        }
        if (_health != null)
        {
            _health.OnDied += OnDied;
            _health.OnDamaged += OnDamaged;
        }
    }

    void OnDisable()
    {
        if (_brain != null)
        {
            _brain.StateChanged -= OnBrainStateChanged;
            _brain.AttackExecuted -= OnAttackExecuted;
        }
        if (_health != null)
        {
            _health.OnDied -= OnDied;
            _health.OnDamaged -= OnDamaged;
        }
    }

    void LateUpdate()
    {
        if (_animator == null || _brain == null) return;

        // Dead 状态下只维持 IsDead=true，Speed 不再更新（死亡动画自己播完就行）
        if (_brain.IsDead)
        {
            if (!_wasDead)
            {
                _animator.SetBool(_deadHash, true);
                _wasDead = true;
            }
            return;
        }

        // 用 position 差 + dt 算当前瞬时速度（世界空间，XZ 平面）
        Vector3 delta = transform.position - _lastPos;
        delta.y = 0f;
        float instantSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

        // 阈值过滤 + 上限截断
        if (instantSpeed < speedDeadzone) instantSpeed = 0f;
        instantSpeed = Mathf.Clamp(instantSpeed, 0f, maxSpeed);

        // SmoothDamp 让 Speed 曲线平滑（PlayerAnimator 也是这个做法）
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, instantSpeed, ref _velRef, speedSmoothTime);
        _animator.SetFloat(_speedHash, _currentSpeed);

        _lastPos = transform.position;
    }

    // ==================== 事件回调 ====================

    private void OnBrainStateChanged()
    {
        // 目前 StateChanged 主要用来"确保 IsDead 复位"这种一次性动作；
        // 攻击/受击/死亡都用各自更精确的事件，不在每帧 SetSpeed 这里塞 Bool。
        if (_brain == null) return;

        if (_brain.IsDead)
        {
            _animator?.SetBool(_deadHash, true);
            _wasDead = true;
        }
    }

    private void OnAttackExecuted()
    {
        if (_animator == null || _brain == null || _brain.IsDead) return;
        _animator.SetTrigger(_attackHash);
    }

    private void OnDied()
    {
        if (_animator == null) return;
        _animator.SetBool(_deadHash, true);
        _wasDead = true;
    }

    private void OnDamaged(DamageInfo info)
    {
        if (_animator == null || _brain == null || _brain.IsDead) return;
        _animator.SetTrigger(_hurtHash);
    }

    // ==================== IPoolable ====================

    public void OnSpawned()
    {
        // 池复用必须 Reset 所有 Animator 参数，避免沿用"死亡/攻击中"的旧状态
        if (_animator != null)
        {
            _animator.SetBool(_deadHash, false);
            _animator.SetFloat(_speedHash, 0f);
            _animator.ResetTrigger(_attackHash);
            _animator.ResetTrigger(_hurtHash);
        }
        _wasDead = false;
        _currentSpeed = 0f;
        _lastPos = transform.position;
    }

    public void OnDespawned() { }

    // 给 SmoothDamp 占位；Unity 2022+ 有独立的 SmoothDamp ref 重载，
    // 这里用一个私有字段保存速度
    private float _velRef;
}
