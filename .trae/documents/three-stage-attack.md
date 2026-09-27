# 三段攻击功能 — 实施计划

## Context（背景与目标）

当前 `PlayerCombat.cs` 是空壳（仅有 `attackCooldown` 字段和两个空方法），`PlayerController` 未集成它，Animator Controller 无攻击 Layer/参数。但 InputSystem 的 `Fire` action 已配置，且 `Assets/Main/Player/Anims/` 下已有 6 个攻击动画片段（Attack-01/02/03 + 对应 End）。

目标：在 PlayerStatus 现有框架上实现三段连击攻击，新增独立 Attack Layer 与原系统低耦合，主要填充 Combat 脚本，尽量不触碰其他脚本。

## 改动范围

| 文件                      | 改动级别         | 说明                                              |
| ----------------------- | ------------ | ----------------------------------------------- |
| **PlayerCombat.cs**     | 完全填充（原为空壳）   | 核心攻击逻辑：连击状态机、动画触发、移动锁定                          |
| **PlayerController.cs** | 最小侵入（\~5行）   | 加 `_combat` 字段 + Awake 获取 + Update 调用 + 攻击时输入归零 |
| **PlayerAnimator.cs**   | 不改           | —                                               |
| **PlayerLocomotion.cs** | 不改           | 复用现有 `SetStopAnimating` 锁移动                     |
| Animator Controller     | Inspector 配置 | 新增 Attack Layer + AttackTrigger 参数 + 7个状态       |
| InputSystem             | 不改           | Fire action 已就绪                                 |

## 一、Animator 新 Layer 设计

### 状态图（Attack Layer，Weight=1，Override，无 Mask）

```
[Empty] ──AttackTrigger──> [Attack1] ──ExitTime 1.0──> [Attack1End] ──ExitTime 1.0──> [Empty]
                                            │
                                            └──AttackTrigger──> [Attack2] ──ExitTime 1.0──> [Attack2End] ──ExitTime 1.0──> [Empty"]
                                                              │
                                                              └──AttackTrigger──> [Attack3] ──ExitTime 1.0──> [Attack3End] ──ExitTime 1.0──> [Empty]
```

* **Empty**：Default State，不挂 Motion，让 Base Layer 透出

* **AttackN → AttackNEnd**：Exit Time=1.0，无 condition（动画自然播完进入收招）

* **AttackNEnd → Empty**：Exit Time=1.0，无 condition（收招完回空转）

* **AttackNEnd → Attack(N+1)**：condition = `AttackTrigger`（连击窗口内按键则进下一段）

* 所有 transition 的 Transition Duration ≈ 0.1s

### 参数（Inspector 手动添加，仅1个）

| 参数名             | 类型      | 用途                                                     |
| --------------- | ------- | ------------------------------------------------------ |
| `AttackTrigger` | Trigger | 驱动 Empty→Attack1、Attack1End→Attack2、Attack2End→Attack3 |

无需 Int/Bool 参数——combo 段数由 C# 代码侧管理。

## 二、PlayerCombat.cs 核心逻辑

### 连击窗口设计

* **Attack 动画中段（normalizedTime ≥ 0.4）**：开始接受连击输入

* **End 动画全程**：继续接受连击输入，播完无人按则回 Empty

* 玩家在非窗口期早按 → `_comboBuffered=true` 缓冲，待进入窗口时自动触发

### 签名级接口

```csharp
public class PlayerCombat : MonoBehaviour
{
    [Header("连击窗口")]
    [SerializeField] private float comboWindowStart = 0.4f;  // Attack 动画中接受输入的 normalizedTime 起点
    [SerializeField] private float endThreshold = 0.95f;    // End 动画结束阈值

    private Animator _animator;
    private PlayerLocomotion _locomotion;
    private PlayerController _playerController;
    private static readonly int AttackTriggerHash = Animator.StringToHash("AttackTrigger");
    private int _comboIndex;        // 0=空闲, 1/2/3=当前段
    private bool _isAttacking;
    private bool _comboBuffered;

    public bool IsAttacking => _isAttacking;

    void Awake();            // GetComponent 三件套 + GetComponentInChildren<Animator>
    public void TryAttack();  // 由 PlayerController 调用，Fire.WasPressedThisFrame() 时触发
    public void UpdateAttack(); // 每帧检测 normalizedTime，管理解锁/重置/缓冲推进
}
```

### TryAttack() 逻辑

1. 若 `_isAttacking == false`（空闲）→ SetTrigger、`_comboIndex=1`、`_isAttacking=true`、`_locomotion.SetStopAnimating(true)` 锁移动
2. 若正在攻击且在连击窗口内（`normalizedTime >= comboWindowStart` 且 `_comboIndex < 3`）→ SetTrigger、`_comboIndex++`
3. 若正在攻击但不在窗口内 → `_comboBuffered = true`（缓冲）

### UpdateAttack() 逻辑

1. 用 `_animator.GetCurrentAnimatorStateInfo(1)` 获取 Attack Layer 当前状态（layer index = 1，因为 Base Layer 是 0）
2. 检测状态名含 "End" 且 `normalizedTime >= endThreshold`：

   * 若 `_comboBuffered` 且 `_comboIndex < 3` → SetTrigger 推进下一段（comboBuffered 清除）

   * 若 `_comboIndex == 3` 或无缓冲 → 结束：`_locomotion.SetStopAnimating(false)`、`_comboIndex=0`、`_isAttacking=false`
3. 注意：需要用 `IsName()` 判断当前状态，因为 normalizedTime 是基于当前播放状态计算的

## 三、PlayerController.cs 改动（最小侵入）

```csharp
// 字段（加1行）
public PlayerCombat _combat;

// Awake（加1行）
_combat = GetComponent<PlayerCombat>();

// Update（改动约3行）
Vector2 move = (_combat != null && _combat.IsAttacking) ? Vector2.zero : input.Player.Move.ReadValue<Vector2>();
_locomotion.SetMoveInput(move);
// ... 末尾追加：
_combat?.TryAttack();
_combat?.UpdateAttack();
```

**为什么攻击时输入归零**：`PlayerLocomotion.ProcessStopAnimationLock()` 有个特性——锁定期间若玩家持续推摇杆，会检测到 `!_wasMoving && IsMoving` 并自动解锁+触发 ForceCancelStop。所以攻击锁移动期间必须保证 moveInput 为零，否则锁会失效。这是1行代码就能解决的问题，且不修改 PlayerLocomotion 本身。

## 四、低耦合性保障

1. **Animator 层级隔离**：攻击在独立 Layer，Base Layer 的 Walk/Idle/Start/Stop 状态机完全不受影响
2. **移动锁定复用**：调 `SetStopAnimating(true/false)` 是 PlayerLocomotion 已有的 public 方法，不改其代码
3. **事件不依赖**：PlayerCombat 不订阅/不发送 PlayerLocomotion 的事件，避免双向耦合
4. **PlayerAnimator 零改动**：攻击参数由 PlayerCombat 自己 Set，不经过 PlayerAnimator

## 五、实施步骤

1. **填充 PlayerCombat.cs**：实现连击状态机、normalizedTime 检测、移动锁定
2. **修改 PlayerController.cs**：加 \_combat 字段 + Awake 获取 + Update 调用 + 输入归零
3. **Animator Controller 配置（Inspector 操作）**：

   * 新建 Layer "Attack"（Weight=1, Override, 无 Mask）

   * 添加参数 `AttackTrigger`（Trigger）

   * 创建 7 个 State：Empty(默认, 无Motion)、Attack1/Attack1End、Attack2/Attack2End、Attack3/Attack3End

   * 拖入对应 .anim（Attack-01→Attack1，Attack-01-End→Attack1End，依此类推）

   * 连 transition：按状态图，condition 只在 Empty→Attack1、Attack1End→Attack2、Attack2End→Attack3 用 AttackTrigger，其余 Exit Time=1.0

## 六、验证方式

1. **编译**：Unity Console 无编译错误
2. **功能测试（Play 模式）**：

   * 闲置按左键 → 播放 Attack1 → Attack1End → 回 Empty

   * Attack1End 期间按左键 → 接 Attack2 → Attack2End → 回 Empty

   * Attack2End 期间按左键 → 接 Attack3 → Attack3End → 回 Empty

   * 连续快速按3次左键 → 三段连击完整播放

   * 攻击期间推摇杆 → 角色不移动（移动锁生效）

   * 攻击结束后推摇杆 → 移动恢复正常
3. **回归测试**：移动/起步/停步/相机/动画行为与改动前一致

