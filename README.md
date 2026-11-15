# RPG · 第三人称动作 RPG 技术演示

> 一个以「**可扩展的模块化框架**」为目标、而不是「堆功能」的 Unity 3D 动作 RPG 技术 Demo。
> 每个系统都按「**接口契约 → 通用实现 → 具体子类 → 数据资产**」四层拆分，设计理由写在代码注释里，本 README 负责把它们串起来。

**当前状态**

| 层 | 进度 |
|---|---|
| 框架层（生命值 / 战斗 / 流程 / UI / 对象池 / 斜面判定 / 相机） | ✅ 完成 |
| 内容层（AI 表现 / 音频 / 存档 / 背包 / 对话 / 关卡目标） | 🚧 进行中 |

<!-- TODO: 放一张 3~5 秒的游戏 GIF 或截图，面试官第一眼就看这个 -->
<!-- ![gameplay](Docs/preview.gif) -->

---

## 目录

- [1. 项目信息](#1-项目信息)
- [2. 快速开始](#2-快速开始)
- [3. 目录结构](#3-目录结构)
- [4. 架构总览](#4-架构总览)
- [5. 核心模块](#5-核心模块)
- [6. 关键调用链](#6-关键调用链)
- [7. 设计决策与取舍](#7-设计决策与取舍)
- [8. 数据配置一览](#8-数据配置一览)
- [9. 扩展指南](#9-扩展指南)
- [10. 已知限制与 TODO](#10-已知限制与-todo)
- [11. 性能说明](#11-性能说明)
- [12. 面试讲解索引](#12-面试讲解索引)
- [13. 第三方资源](#13-第三方资源)
- [14. 联系](#14-联系)

---

## 1. 项目信息

| 项 | 值 |
|---|---|
| Unity 版本 | `2022.3.62f3c1` |
| 渲染管线 | URP `14.0.12`（含 Global Volume 后处理） |
| 输入系统 | Input System `1.14.2`（`Active Input Handling = Both`，新旧共存） |
| 动画 | Animator + StateMachineBehaviour，`Apply Root Motion = false`（位移由代码接管） |
| 物理 | `CharacterController`（玩家） / 直接移动的静态 Collider（敌人，见 §10） |
| 启动场景 | `Assets/Scenes/01-GameScene.unity`（Build Settings 中唯一场景） |
| 目标平台 | PC / Windows <!-- TODO: 补上实际出包验证过的平台 --> |
| 游戏循环 | 主菜单 → 游玩 → 死亡 → 复活/重开 → 返回主菜单，闭环可玩 |

---

## 2. 快速开始

### 打开与运行

1. Unity Hub 用 **2022.3.62f3c1** 打开本项目；
2. 打开 `Assets/Scenes/01-GameScene.unity`；
3. 点 Play，点主菜单的「开始游戏」。

> 首次打开会编译 TextMesh Pro / URP 资产，属正常现象。

### 操作说明

| 输入 | 行为 | 输入绑定 |
|---|---|---|
| `WASD` / 方向键 | 八向移动 | `PlayerControls.Player.Move` |
| `Left Shift` | 奔跑 | `Sprint` |
| 鼠标移动 | 第三人称视角 | `Look` |
| 鼠标左键 | 攻击 / 二段连击 | `Fire` |
| `Esc` | 暂停 / 继续 | `GameFlowProfile.pauseKey` |
| `T` | 对自己造成 20 真实伤害（测试用） | `HealthTestInput` |

> `HealthTestInput` 整体包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 里，正式发布包不参与编译。

---

## 3. 目录结构

```
Assets/
├── Main/                                 # 全部自研代码，按业务域划分
│   ├── Common/                           # 与业务无关的基础设施
│   │   ├── IPoolable.cs                  # 池化对象生命周期契约
│   │   ├── ObjectPool.cs                 # 泛型池核心（纯 C# 类，非 MonoBehaviour）
│   │   └── PoolManager.cs                # 按「预制体」分池的全局注册表
│   │
│   ├── Health/                           # 生命值域（玩家/敌人/可破坏物通用）
│   │   ├── HealthScripts/
│   │   │   ├── IHealthSystem.cs          # 只读状态 + 事件 + 治疗（UI/技能用）
│   │   │   ├── IDamageable.cs            # 受伤入口（攻击方用）
│   │   │   ├── DamageInfo.cs             # readonly struct 伤害包 + DamageType
│   │   │   ├── DamageCalculator.cs       # 纯静态伤害公式（无状态，可单测）
│   │   │   ├── HealthProfile.cs          # ScriptableObject 配置
│   │   │   └── HealthComponent.cs        # 通用实现（模板方法模式）
│   │   ├── UI/HealthBarUI.cs             # 血条：前景条 + 残影条双值插值
│   │   └── Tests/HealthTestInput.cs      # 调试自伤按键（条件编译剔除）
│   │
│   ├── Player/
│   │   ├── Input/PlayerControls.cs       # .inputactions 生成的 C# 包装
│   │   ├── Anims/                        # Idle / Walk / Run / 起步 / 停步 / 攻击×4
│   │   ├── AnimatorController/           # 双层：Base(移动混合树) + Attack(攻击段)
│   │   └── PlayerScripts/
│   │       ├── PlayerStatus/             # Controller / Locomotion / Animator / Combat / Health
│   │       │   ├── AttackHitDetection.cs # 命中判定（OverlapSphere + 动画窗口）
│   │       │   └── Combat/               # AttackStateBehaviour / EndStateBehaviour（两个 SMB）
│   │       ├── PlayerCamera/ThirdPersonCamera.cs
│   │       ├── PlayerInteract/PlayerInteract.cs   # ⚠️ 未挂载
│   │       └── SlopeDetection/           # 五层分离：Profile / 接口 / 实现 / 消费 / 调试
│   │
│   ├── Enemy/
│   │   ├── EnemyScripts/                 # Brain / Health / Controller / Profile / Spawner
│   │   └── EnemyPrefab/Enemy.prefab      # ⚠️ 占位美术（树模型，无 Animator）
│   │
│   ├── GameFlow/
│   │   ├── GameFlowScripts/              # GameManager / GameState / Profile / 桥接 / 暂停输入
│   │   └── UI/                           # UIManager + GamePanel 基类 + 3 个流程面板
│   │
│   ├── NPC/NPCScripts/NPCInteractable.cs # ⚠️ 仅 Debug 占位
│   ├── Environment/                      # CursorLockManager（已在场景禁用）/ 地面网格
│   └── Items/                            # 🚧 空目录，预留 InventorySystem / ItemDefinitions
│
├── Scenes/01-GameScene.unity
├── ThirdParty/                           # Ellen(URP 预设) / SimpleNaturePack(场景美术)
├── TextMesh Pro/ · Fonts/                # TMP 资源与中文字体
└── ...
```

**注意**：目前代码全部位于 `Assembly-CSharp`，没有 asmdef、没有 namespace（见 §10）。

---

## 4. 架构总览

### 4.1 分层与依赖方向

```
┌──────────────────────────────────────────────────────────────┐
│ 表现层 UI                                                     │
│   UIManager ──订阅 OnStateChanged──▶ GameManager               │
│      ▲                    │                                   │
│      │ 注册 / 受控        │ 面板只调用「意图方法」              │
│   GamePanel(MainMenu / Pause / Death)                         │
│      │ 按钮 → StartGame / Resume / RespawnPlayer / RestartGame │
└──────┼───────────────────────────────────────────────────────┘
       │  单向依赖：UI → 逻辑；逻辑层永不引用任何 UI
┌──────▼───────────────────────────────────────────────────────┐
│ 流程层                                                        │
│   GameManager（全局状态唯一真相源）                            │
│     GameState = MainMenu | Playing | Paused | PlayerDead      │
│     ApplyState() 把「状态」翻译成「环境副作用」：               │
│       Time.timeScale / AudioListener.pause / 光标锁定          │
└──────┬────────────────────────────────────▲──────────────────┘
       │ RegisterPlayer()                   │ NotifyPlayerDied()
┌──────▼────────────────────────────────────┴──────────────────┐
│ 桥接层                                                        │
│   PlayerLifecycleBridge                                       │
│     依赖 IHealthSystem（听死亡） + IRevivable（调复活）         │
│     不认识 PlayerHealth 具体类                                 │
└──────┬───────────────────────────────────────────────────────┘
┌──────▼───────────────────────────────────────────────────────┐
│ 逻辑层                                                        │
│   HealthComponent ──继承──▶ PlayerHealth / EnemyHealth        │
│   PlayerCombat ◀──SMB 回调── AttackStateBehaviour / EndState  │
│   EnemyBrain（轻量 FSM） / AttackHitDetection                 │
│   ObjectPool<T> ← PoolManager ← EnemySpawner                  │
└──────────────────────────────────────────────────────────────┘
```

### 4.2 四条贯穿全项目的原则

1. **逻辑层不认识 UI** —— `GameManager` 只广播事件，不知道面板存在；`HealthComponent` 只发 `OnHealthChanged`，不引用 `HealthBarUI`。
2. **跨模块只认接口** —— `IDamageable` / `IHealthSystem` / `IRevivable` / `IPoolable` / `ISlopeDataProvider`，新增目标类型零改动。
3. **数值外置** —— 所有可调参数进 `ScriptableObject`（4 份 Profile），逻辑类只保留兜底默认值。
4. **事件驱动，不轮询** —— 跨模块通信一律 `event`，禁止每帧读别人的状态字段。

### 4.3 全局流程状态机

```
        ┌───────────┐  StartGame   ┌───────────┐
        │ MainMenu  │─────────────▶│  Playing  │
        └───────────┘              └─────┬─────┘
              ▲                          │ TogglePause
              │ ReturnToMainMenu         ▼
              │                    ┌───────────┐
              │                    │  Paused   │
              │                    └───────────┘
              │                          │
        ┌─────┴───────┐  RespawnPlayer   │
        │ PlayerDead  │◀─────────────────┘ NotifyPlayerDied
        └─────────────┘
任意状态 ──RestartGame / ReturnToMainMenu──▶ 重载场景
              （静态 _pendingStateAfterReload 还原「重开后进哪个状态」）
```

---

## 5. 核心模块

### 5.1 生命值 Health

**职责**：一份血量实现，玩家 / 敌人 / 未来 NPC 与可破坏物全部复用。

| 契约 | 谁依赖它 | 暴露什么 |
|---|---|---|
| `IHealthSystem` | UI、治疗技能、任务系统 | `MaxHealth` `CurrentHealth` `IsAlive` `OnHealthChanged` `OnDied` `Heal()` |
| `IDamageable` | 攻击方（玩家/敌人） | `TakeDamage(DamageInfo)` `OnDamaged` |
| `IRevivable` | 游戏流程层 | `Revive()` |

**关键设计**

- **单一受伤入口**：所有伤害必须走 `HealthComponent.TakeDamage(DamageInfo)`，外部拿不到写血量的能力（`IHealthSystem` 里没有 setter，也没有 `TakeDamage`）。
- **受伤管线顺序**：合法性校验（NaN/Infinity/≤0）→ 死亡锁 → 伤害修正（护甲/真实伤害）→ 钳制扣血 → `OnDamaged` → `OnHealthChanged` → 死亡判定 → `HandleDeath()`。
- **伤害公式**（`DamageCalculator`，纯静态无状态）：
  ```
  减伤率 mitigation = armor / (armor + 100)   // 边际递减，永远到不了 100%
  最终伤害 = max(1, rawDamage × (1 - mitigation))  // 保底 1 点，保证命中有反馈
  真实伤害（DamageType.True）无视护甲
  ```
- **两个 virtual 扩展点**：`ModifyIncomingDamage()`（护盾/格挡/易伤）和 `HandleDeath()`（死亡表现/掉落），子类重写不改父类。
- **池化兼容**：`HealthComponent.Start()` 显式广播一次初始血量，保证订阅方 `OnEnable` 已就绪；池化复用不会重跑 `Start`，因此子类用 `NotifyHealthChanged()` 手动补广播。

**血条** `HealthBarUI` 用三条值做表现：`targetFill`（逻辑真值）/ `frontFill`（红条，指数阻尼追赶）/ `ghostFill`（白色残影，受伤后延迟再追），全部按 `1 - e^(-k·dt)` 做帧率无关插值；治疗时残影直接同步，不反向拖尾。

### 5.2 战斗 Combat

**职责**：两段连击的状态推进与命中判定，**动画状态机负责"时机"，代码只负责"意图"**。

| 文件 | 职责 |
|---|---|
| `PlayerCombat.cs` | 连击阶段机（`Idle / Attacking / Recovery`）、连击窗口、输入缓冲 |
| `AttackStateBehaviour.cs` | 挂在 `Attack1/Attack2` 上的 SMB，`OnStateEnter/Exit` 通知代码 |
| `EndStateBehaviour.cs` | 挂在 `Attack1End/Attack2End` 上的 SMB，收刀结束通知 |
| `AttackHitDetection.cs` | 按动画 `normalizedTime` 窗口做一次 `OverlapSphere`，只调 `IDamageable` |

**Animator 结构**（`PlayerAnimatorController.controller`）

```
Base Layer   : Locomotion(Idle_BT / Walk_BT / Run_BT) ⇄ Walk_Start / Walk_End / Run_End
Attack Layer : Empty → Attack1 → Attack1End → Empty
                      └→ Attack2 → Attack2End → Empty      (ExitTime = 1 自动过渡)
参数          : Speed(float) IsStarting/IsStopping(bool) StopType(int)
                AttackTrigger/MoveTrigger(trigger)
```

**为什么用 SMB 而不是 `Update` 轮询 `normalizedTime`**

> "播完没有"由状态机自己保证，`OnStateExit` 一定在过渡发生时回调；代码只在"能不能取消"这类**语义判断**上读 `normalizedTime`。这样避免了 Attack→Empty→End 中间帧的误判。

**手感参数**

| 参数 | 默认值 | 含义 |
|---|---|---|
| `comboWindowStart` | 0.4 | 动画播到 40% 后允许取消进下一段 |
| `inputBufferTime` | 0.15s | 窗口外按攻击会被缓存，收刀结束后立即起手 |
| 一段/二段伤害 | 20 / 30 | `AttackHitDetection` |
| 命中窗口 | 0.35 ~ 0.65 | 每段只判定一次（`_hitDone` 锁） |
| 命中球 | 半径 0.8，偏移 (0, 1, 0.9) | 只打 Layer 7（Enemy） |

### 5.3 游戏流程 GameFlow

**职责**：把「游戏现在处于哪个阶段」收敛为一个枚举，并把这个枚举翻译成环境副作用。

| 文件 | 职责 |
|---|---|
| `GameState.cs` | 四态枚举，单一真相源 |
| `GameManager.cs` | 状态切换 + 副作用应用 + 对外意图方法 |
| `GameFlowProfile.cs` | SO 配置（初始状态/暂停键/音频/光标/复活延迟/重启方式） |
| `PlayerLifecycleBridge.cs` | 玩家血量 ↔ 流程系统的解耦桥 |
| `PauseInputHandler.cs` | 按键 → 暂停意图 |

**对外意图方法**（UI 只能调这些）

```csharp
StartGame()  TogglePause()  Resume()  NotifyPlayerDied()
RespawnPlayer()  RestartGame()  ReturnToMainMenu()
RegisterPlayer(bridge)  UnregisterPlayer(bridge)
```

**`ApplyState()` 承担的副作用**

```csharp
Time.timeScale   = (state == Playing) ? 1f : 0f;   // 非游玩态冻结世界
AudioListener.pause = !playing;                    // 可配
Cursor.lockState / Cursor.visible                  // 游玩时锁定并隐藏
_respawnTimer    = profile.autoRespawnDelay;       // 进入死亡态启动计时
```

> `Update` 里的复活倒计时用 `Time.unscaledDeltaTime`，因为死亡时 `timeScale = 0`。
> `LateUpdate` 里每帧收敛光标，防止相机/其它脚本抢锁。

### 5.4 UI 层

**职责**：按流程状态**仲裁**面板显隐，结构上杜绝"面板堆叠/残留"。

| 文件 | 职责 |
|---|---|
| `UIManager.cs` | 面板注册表 + 状态变化统一刷新 + 同状态重复声明告警 |
| `GamePanel.cs` | 抽象基类：声明 `ShownState`、CanvasGroup 淡入淡出、图片接入适配点 |
| `MainMenuPanel / PausePanel / DeathPanel` | 三个具体面板，只把按钮翻译成 `GameManager` 调用 |

**工作方式**

1. 面板在 `OnEnable` 自动向 `UIManager` 注册（无需手动拖引用）；
2. `UIManager` 订阅 `GameManager.OnStateChanged`，状态一变即 `RefreshAll()`；
3. 只有 `ShownState == 当前状态` 的面板显示，**其余全部隐藏**（互斥，无残留）；
4. 注册时两面板声明同一状态会立即 `LogWarning`，把配置错误消灭在启动期。

**约定**：面板必须常驻激活，显隐只由 CanvasGroup 的 `alpha / interactable / blocksRaycasts` 控制，这样事件订阅与淡入淡出都稳定；过渡动画用 `unscaledDeltaTime`（暂停/死亡时 `timeScale = 0`）。

### 5.5 对象池

**职责**：消除高频 `Instantiate/Destroy` 带来的 GC 与卡顿。

```
PoolManager (MonoBehaviour, 全局唯一)
   └─ Dictionary<GameObject prefab, ObjectPool<Transform>>
         └─ ObjectPool<T> (纯 C#)：Stack<T> 存空闲实例
               Get():  空栈则 Instantiate → 置位 → SetActive(true) → 逐个 IPoolable.OnSpawned()
               Release(): 逐个 OnDespawned() → SetActive(false) → 挂回池根 → 入栈
```

- **顺序很关键**：`SetActive(true)` 会先触发子组件的 `OnEnable`（事件订阅就位），之后才调 `OnSpawned()`，这样 `OnSpawned` 里广播的血量才不会被还没订阅的 UI 丢掉。
- **`IPoolable` 协议**：`OnSpawned` 等价"第二次 Awake"，必须把对象恢复到刚实例化的状态；`OnDespawned` 清理运行期外部引用；两者必须幂等。
- **兜底**：`Despawn` 收到非池来源（场景手摆的对象）时退化为 `Destroy`，避免污染池的复用逻辑。
- 当前已接入：`Enemy`（`EnemyHealth : IPoolable` + `EnemyBrain : IPoolable`）。

### 5.6 敌人 AI

**职责**：四状态轻量 FSM。

```
Idle  ──目标进入 chaseRange──▶ Chase
Chase ──进入 attackRange─────▶ Attack（进入时立即出手一次）
Chase ──超出 loseTargetRange─▶ Idle（脱战）
Attack──超出 attackRange×1.2─▶ Chase（滞回，防边界抖动）
任意  ──OnDied───────────────▶ Dead
```

- **索敌**：`Physics.OverlapSphereNonAlloc` + `LayerMask`（只找 Layer 6 Player），复用静态缓冲区避免 GC，不硬编码 Tag / 单例。
- **距离口径**：全部用**水平平方距离**（剔除 Y 轴、免开方）。敌人可能半埋做视觉对齐，用三维距离会让 Y 差吃掉整个 `attackRange`。
- **攻击协议**：只调 `IDamageable.TakeDamage(DamageInfo)`，与玩家攻击同一套约定。
- **形态**：`enum + switch` 单文件。当前 4 个状态、每个 ≤10 行，拆成状态类反而增加跳转成本；等状态带独立数据或数量膨胀时再重构成「状态基类 + 一状态一类」。
- **编辑器可视化**：`OnDrawGizmosSelected` 画追击/攻击范围圈。

| 参数（`EnemyProfile`） | 默认值 |
|---|---|
| `moveSpeed` | 2 |
| `attackDamage` | 10 |
| `attackInterval` | 2s |
| `attackRange` | 1.5 |
| `chaseRange` | 8 |
| `loseTargetRange` | 12 |
| `turnSpeed` | 540°/s |

### 5.7 移动与斜面判定

分两块：**移动**（`PlayerLocomotion` 驱动位移）与**斜面判定**（独立模块，只提供数据）。

#### A. 角色移动 `PlayerLocomotion`

- **唯一驱动入口** `UpdateLocomotion()`，由 `PlayerController.Update` 每帧调用，物理计算不外泄。
- **移动方向以相机 Yaw 为基准**：`CacheCameraYaw()` 每帧取主相机 Y 轴旋转，输入向量经它旋转后得到世界方向；相机没指定时回退到自身朝向。
- **转向**：`Mathf.SmoothDampAngle` 平滑插值到目标角，避免瞬间掉头。
- **重力**：`_verticalVelocity` 累积，`isGrounded` 时钳到 `-2f`（保持接地吸附力）。
- **起步/停步动画锁**（`ProcessStopAnimationLock`）：停步动画播放期间把 Move 输入归零、只处理重力；期间玩家再次推杆则立即打断（`OnForceCancelStop`），不会有"卡住动不了"的手感问题。
- **三个事件** 供 `PlayerAnimator` 消费：`OnStartMoving` / `OnStopMoving(bool wasSprinting)` / `OnForceCancelStop`。

```
PlayerController.Update
 ├─ 攻击中? Move 输入归零（但 MoveIntent 保留真实意图）
 ├─ Locomotion.SetMoveInput / SetSprint
 ├─ Locomotion.UpdateLocomotion()   → 位移
 └─ Animator.UpdateAnimParams()     → Speed 参数（阻尼 damp 0.15）
```

#### B. 斜面判定（五层，任意一层可替换）

| 层 | 文件 | 职责 |
|---|---|---|
| 配置 | `SlopeProfile.cs` | 阈值/滞回/层掩码/地面类型映射，全在 SO 里 |
| 接口 | `ISlopeDataProvider.cs` | 只读契约 + `SlopeSnapshot` 快照 |
| 实现 | `SlopeDetector.cs` | 每帧 `SphereCast` 向下检测，内部完全封闭 |
| 消费 | `SlopeModelTilt.cs` | 读 `GroundNormal` 让模型贴合坡度（只转模型子物体，不转根物体） |
| 调试 | `SlopeDebugProbe.cs` | 运行时观测面板 |

**技术点**

- **球扫起点与胶囊底对齐**：起点 = 胶囊底面中心 + up×radius，保证既不陷入地面、也不会检到自己（球整体位于胶囊内）。
- **法线精修**：球扫在斜面边缘会混合法线，命中后再补一条短射线取真实法线。
- **双滞回 + 帧确认**：角度滞回 5° + 连续 2 帧确认，避免在阈值附近高频翻转状态、狂刷事件。
- **快照只读**：外部只拿到 `SlopeSnapshot`（readonly struct），检测策略（CC / Rigidbody）换了接口不变。
- **预留地面类型**：`SlopeProfile.groundTypes` 按 Layer 映射 `surfaceId`（冰面/泥地），消费方按 id 改行为。
- **执行顺序**：`[DefaultExecutionOrder(-10)]` 检测先于移动（0），`SlopeModelTilt` 在 100 的 `LateUpdate` 里执行以避开 `Rotate()`。

### 5.8 相机

`ThirdPersonCamera`：自研弹簧臂。

- `LateUpdate` 执行，避免与角色移动打架；
- 俯仰钳制 -25°~60°，`SmoothDampAngle` 平滑转向；
- **弹簧臂碰撞**：从目标点向相机方向 `Raycast`，命中则收缩距离（`hit.distance - 0.3`，不低于 `minDistance`）；
- **防御性 NaN 过滤**：输入系统在场景重载时序下可能产出 NaN/Infinity，污染 `SmoothDamp` 的 velocity 后会永久传播，因此在读输入、写 transform 前后都做了 `IsValid` 校验，并支持状态自愈复位。
- ⚠️ Cinemachine 也已安装但**未接入**（见 §10）。

### 5.9 输入

- `PlayerControls.cs` 由 `.inputactions` 生成，暴露 `Player/Move`、`Look`、`Sprint`、`Fire`；
- `PlayerController` 每帧把输入翻译成 `Locomotion.SetMoveInput / SetSprint`，并在攻击期间把 Move 归零（但 `MoveIntent` 保留真实意图供 `PlayerCombat` 判断"玩家想跑"）；
- UI 用 `InputSystemUIInputModule`（新输入系统的 EventSystem）；
- ⚠️ `PlayerInteract`、`PauseInputHandler`、`HealthTestInput` 仍在用旧版 `Input.GetKeyDown`（`Active Input Handling = Both` 才能跑）。

---

## 6. 关键调用链

### 6.1 一次普攻

```
按下左键
 └─ PlayerControls.Player.Fire
     └─ PlayerController.Update → PlayerCombat.TryAttack()
         ├─ Idle        → StartAttack(1)        → SetTrigger("AttackTrigger")
         ├─ Attacking   → 窗口内: AdvanceCombo() → SetTrigger("AttackTrigger")
         │              窗口外: BufferInput()    → 缓存 0.15s
         └─ Recovery    → BufferInput()
             ↓ Animator 过渡到 Attack1 / Attack2（Attack Layer）
         AttackStateBehaviour.OnStateEnter
             └─ PlayerCombat.NotifyAttackEnter(stage)
                 └─ PlayerLocomotion.SetStopAnimating(true)      // 攻击期锁移动
             ↓ 动画播到 0.35 ~ 0.65
         AttackHitDetection.Update
             └─ PerformHit(stage)  (OverlapSphere, Layer 7, 每段一次)
                 └─ IDamageable.TakeDamage(DamageInfo)
                     └─ HealthComponent：校验 → 护甲修正 → 扣血
                         → OnDamaged → OnHealthChanged（UI 刷新）→ 死亡判定
             ↓ ExitTime = 1
         AttackStateBehaviour.OnStateExit → NotifyAttackExit()   // phase = Recovery
         └─ Attack1End → EndStateBehaviour.OnStateExit → NotifyEndExit()
             ├─ 有缓冲 → StartAttack(1)
             └─ 无缓冲 → EndAttack() → SetStopAnimating(false)   // 解锁移动
```

### 6.2 玩家死亡 → 复活

```
HealthComponent.TakeDamage → 血量归零
 └─ _isAlive = false → OnDied
     ├─ PlayerHealth.HandleDeath()  被 override 成空 → 不销毁对象（要原地复活）
     └─ PlayerLifecycleBridge.HandlePlayerDied() → GameManager.NotifyPlayerDied()
         └─ SetState(PlayerDead)
             ├─ ApplyState: timeScale=0 / 音频暂停 / 释放光标 / 启动复活计时
             └─ OnStateChanged → UIManager.RefreshAll() → DeathPanel 淡入

点击「复活」
 └─ GameManager.RespawnPlayer()
     ├─ PlayerLifecycleBridge.RevivePlayer()
     │   ├─ IRevivable.Revive()      // 数据层：满血 + 解锁 + 1.5s 无敌 + 广播血条
     │   └─ Teleport()               // 空间层：先禁用 CC → 赋值 → 再启用
     └─ SetState(Playing) → timeScale=1 → 面板互斥隐藏
```

### 6.3 敌人死亡 → 回池

```
HealthComponent.TakeDamage → 归零 → OnDied → EnemyBrain.HandleDied → State.Dead
 └─ EnemyHealth.HandleDeath() 被 override → Invoke(ReturnToPool, destroyDelay)
     └─ PoolManager.Despawn(gameObject)
         └─ ObjectPool.Release() → IPoolable.OnDespawned() → SetActive(false) → 入栈

下次 EnemySpawner.SpawnWave()
 └─ PoolManager.Spawn() → ObjectPool.Get()
     ├─ SetActive(true)                 // 子组件 OnEnable 先就位（重新订阅事件）
     ├─ EnemyHealth.OnSpawned → ResetState()  // 满血 + 解锁 + 手动广播血条
     └─ EnemyBrain.OnSpawned  → State.Idle     // 复位状态机与攻击计时
```

---

## 7. 设计决策与取舍

> 这张表是本项目最值得讲的部分：每个决策都有明确的**理由**和**代价**。

| # | 决策 | 理由 | 代价 / 何时该改 |
|---|---|---|---|
| 1 | 伤害只有一个入口 `TakeDamage(DamageInfo)` | 无法绕过校验/事件链直接改血量 | 单一职责压在一个方法上，逻辑膨胀时要拆管线 |
| 2 | `IHealthSystem` 与 `IDamageable` 拆开 | 接口隔离：UI/技能只读，拿不到伤害能力 | 接口变多，新人要理解契约分工 |
| 3 | `ModifyIncomingDamage` / `HandleDeath` 设 `protected virtual` | 开闭原则：护盾/无敌/掉落都靠重写 | 继承层次深了会变成"父类地狱"，届时改组合（策略对象） |
| 4 | 攻击时机用 SMB 而非轮询 `normalizedTime` | 状态机保证 `OnStateExit` 时机准确 | 依赖 Animator 配置；改了状态机要同步改 SMB 挂载 |
| 5 | 连击窗口 + 0.15s 输入缓冲 | 手感：玩家提前按键不丢 | 缓冲时长是经验值，需真机调 |
| 6 | 全局流程收敛为单个 `GameState` 枚举 | 杜绝 `isPaused/isDead/isMenuOpen` 组合不一致 | 新增状态要同时改枚举 + `ApplyState` |
| 7 | 副作用集中在 `ApplyState()` | 状态变化只有一处产生副作用，可审计 | 所有环境依赖（timeScale/音频/光标）耦合在一个方法里 |
| 8 | 面板"声明自己在哪显示"，由 `UIManager` 仲裁 | 结构上不可能出现两个全屏面板 | 面板不能自己控制显隐（刻意的限制） |
| 9 | 面板只调意图方法，不改状态、不碰 `timeScale` | 状态唯一真相源 | 面板需要读状态时只能订阅事件 |
| 10 | 对象池 + `IPoolable` 协议 | 避免高频 Instantiate/Destroy 的 GC 峰值 | 每个池化类型都要正确实现 `OnSpawned/OnDespawned`，漏了就出诡异 bug |
| 11 | 池化对象**手动**广播血量 | 复用不走 `Start`，UI 不会自动收到初始值 | 必须在 `ResetState` 里显式调用，忘记就血条不同步 |
| 12 | 所有数值进 ScriptableObject | 策划改参数不用碰代码、不用改预制体；多敌人可共享同一份血量配置 | 资产多了要管理；运行时改会污染资产（需实例化） |
| 13 | 敌人 FSM 用 `enum + switch` 不拆类 | 4 个状态、每个 ≤10 行，跳转成本最低 | 状态 >6 或状态要带独立数据时必须重构 |
| 14 | 距离判定全用平方距离 + 水平投影 | 省开方；剔除 Y 差避免"贴身但进不了攻击态" | 飞行/高低差 AI 需另开高度判定 |
| 15 | 斜面用 `SphereCast` + 球底对齐 + 法线精修 | 球扫不卡边缘、不检自身、斜面法线准确 | 每帧一次球扫，角色数量多时要限频 |
| 16 | 单例 + `FindFirstObjectByType` 惰性兜底 | 规避脚本执行顺序导致的隐性 bug | 隐式耦合、难单测；规模大了该换 DI / Service Locator |
| 17 | 主菜单与游戏在**同一场景**（`timeScale = 0` 冻结） | 无需场景切换开销，重开=重载场景 | 场景变大后加载慢；正式项目应拆场景 + Addressables |

---

## 8. 数据配置一览

| 资产 | 路径 | 关键字段（默认值） |
|---|---|---|
| `HealthProfile` | `Assets/Main/Health/HealthScripts/HealthProfile.asset` | `maxHealth` 100 · `destroyDelay` 2 · `showHealthBar` · `worldSpaceUI` |
| `EnemyProfile` | `Assets/Main/Enemy/EnemyScripts/EnemyProfile.asset` | `healthProfile`(引用) · `moveSpeed` 2 · `attackDamage` 10 · `attackInterval` 2 · `attackRange` 1.5 · `chaseRange` 8 · `loseTargetRange` 12 |
| `GameFlowProfile` | `Assets/Main/GameFlow/GameFlowScripts/GameFlowProfile.asset` | `startState` MainMenu · `pauseKey` Escape · `pauseAudioListener` · `lockCursorWhilePlaying` · `autoRespawnDelay` 0 · `restartReloadsScene` true |
| `SlopeProfile` | `Assets/Main/Player/PlayerScripts/SlopeDetection/SlopeProfile.asset` | `maxSlopeAngle` 45 · `minSlopeAngle` 10 · `angleHysteresis` 5 · `confirmFrames` 2 · `castDistance` 0.35 · `castRadiusFactor` 0.85 · `groundMask` |

**Layer 约定**

| Layer | 名称 | 用途 |
|---|---|---|
| 6 | `Player` | 玩家根物体；`EnemyBrain.targetMask` 只搜这一层 |
| 7 | `Enemy` | 敌人；`AttackHitDetection.hitMask` 只打这一层 |

> 创建新资产：Project 右键 → `Create → RPG → Health / Enemy / Game Flow / Slope Profile`。

---

## 9. 扩展指南

### 新增一种敌人（如远程怪）

1. 复制 `Enemy.prefab`；
2. 新建 `EnemyProfile` 资产填数值，或直接共享已有的 `HealthProfile`；
3. 若要新行为：继承 `HealthComponent` 或给 `EnemyBrain` 加状态 —— **不要改 `AttackHitDetection` / `HealthComponent`**；
4. 拖到 `EnemySpawner.enemyPrefab`。

### 新增一个全局流程状态（如过场/结算）

1. `GameState` 追加枚举值；
2. `GameManager.ApplyState()` 补一条副作用规则。注意当前 `bool playing = state == Playing` 同时决定 `timeScale` / 音频 / 光标，新状态要先想清楚它算不算 "playing"（比如过场要冻结世界但保留音频，就得把音频判定单独拆出来）；
3. 新建面板继承 `GamePanel` 并重写 `ShownState` → `GameManager` / `UIManager` **零改动**。

### 新增一个 UI 面板

```csharp
public class SettingsPanel : GamePanel
{
    public override GameState ShownState => GameState.Paused;  // ⚠️ 与 PausePanel 冲突会告警
}
```
> 当前是"互斥显示"模型，同屏叠加（HUD + 弹窗）需要扩展 `UIManager`，`sortingPriority` 已为此预留。

### 新增一种可池化对象（子弹 / 伤害数字 / 掉落物）

1. 调 `PoolManager.Instance.Spawn(prefab, pos, rot)` 生成；
2. 组件实现 `IPoolable`：`OnSpawned()` 复位状态、`OnDespawned()` 清理引用；
3. 归还调 `PoolManager.Instance.Despawn(go)`。`PoolManager` 与 `ObjectPool` 零改动。

### 新增伤害类型 / 抗性 / 暴击

- 抗性：在 `DamageCalculator.Calculate` 加 `multiplier` 参数或按 `DamageType` 查表；
- 暴击：调用方算好后填进 `DamageInfo.Amount`，受击方不需要知道；
- 护盾：`PlayerHealth` / `EnemyHealth` 重写 `ModifyIncomingDamage()`，先扣盾再 `base.`。

---

## 10. 已知限制与 TODO

> 面试时主动讲出来，比被问出来好。逐条修掉后就从这份列表里删除。

### 待修（按优先级）

- [ ] **无单元测试**：`test-framework 1.1.33` 已安装，但 `Assets` 下 `[Test]` 出现 0 次。`DamageCalculator` / `HealthComponent` / `ObjectPool` 都是现成的 EditMode 用例目标，且需先加 `Tests` asmdef。
- [ ] **零音效**：全项目无 `AudioSource`/`AudioClip`。缺挥砍、命中、受伤、死亡、UI 点击 + AudioMixer。
- [ ] **敌人无动画、无血条、模型是树**：`Enemy.prefab` 无 `Animator`，子物体是 `Tree_04_LOD0/1`；`HealthProfile.showHealthBar` / `worldSpaceUI` / `EnemyHealth.playHurtAnimation` 均是**无人消费的死字段**。
- [ ] **敌人不用 NavMesh**：`EnemyBrain` 直接 `transform.position +=`，会穿墙、互相重叠，且每帧移动无 `Rigidbody` 的静态 Collider 触发物理重建。AI Navigation 包已装但未使用。
- [ ] **`PlayerInteract` 未挂载**：组件不在场景 Player 上（`01-GameScene.unity` 的 Player 组件列表中无此脚本），按 `E` 无任何反应；且它用旧输入、每次按键 `OverlapSphere` 分配数组、无 LayerMask、多 NPC 会全部触发。
- [ ] **`NPCInteractable.Interact()` 只有 `Debug.Log`**：对话/任务系统未做。
- [ ] **`CursorLockManager` 死代码**：场景中已 `m_Enabled = 0` 禁用（光标已由 `GameManager` 接管），脚本本体应删除。
- [ ] **Cinemachine 装而未用**：`Main Camera` 上无 `CinemachineBrain`，Player 下残留一个只挂 `CinemachinePipeline` 的 `cm` 空物体。应清理依赖或正式接入。
- [ ] **`PlayerAnimator` 动画时长硬编码**：`_walkStartDuration` 等三个值 + `WaitForSeconds`，改动画剪辑后会自动不同步。应改用 SMB 或 `clip.length`。
- [ ] **新旧输入混用**：`PlayerInteract` / `PauseInputHandler` / `HealthTestInput` 用旧 `Input`，靠 `Both` 模式兼容。
- [ ] **无 HUD**：没有击杀数、目标提示、技能冷却、伤害数字飘字。
- [ ] **无战斗表现层**：无 hitstop（顿帧）、无镜头抖动、无击退、无受击硬直（`EnemyBrain` 预留了 HurtState 但未实现）。

### 未开始

- [ ] 存档 / 读档（JSON + 版本号迁移）
- [ ] 背包 / 装备 / 属性面板（`Assets/Main/Items/` 已建空目录）
- [ ] 技能与冷却、资源条
- [ ] 经验 / 升级
- [ ] 多波次刷怪 / 精英怪 / Boss
- [ ] 设置菜单（音量 / 画质 / 灵敏度）
- [ ] 多场景 + Addressables（当前单场景 + 重载）

### 工程规范

- [ ] 无 asmdef、无 namespace，46 个类全在 `Assembly-CSharp` 全局命名空间
- [ ] 无 `Profiler` 数据/截图，性能优化缺少证据
- [ ] Git 历史只有 1 次 `v0.0.5` 提交，看不出迭代过程
- [ ] 无 `.gitattributes` / LFS（fbx、png 直接进 Git）
- [ ] 未出过包，`companyName` 仍是 `DefaultCompany`

---

## 11. 性能说明

**已做的**

| 手段 | 位置 | 效果 |
|---|---|---|
| 对象池复用敌人 | `PoolManager` / `ObjectPool<T>` | 消除波次刷怪的 Instantiate/Destroy GC 峰值 |
| 物理查询 `NonAlloc` | `EnemyBrain._targetBuffer` / `AttackHitDetection._hitBuffer` | 索敌与命中判定零分配 |
| 平方距离替代 `Distance` | `EnemyBrain` | 每帧省 2 次开方 |
| 参数哈希替代字符串 | `PlayerCombat` / `PlayerAnimator` 的 `Animator.StringToHash` | 避免每帧字符串查表 |
| 事件替代轮询 | 全局 | 无每帧 `GetComponent` / 状态扫描 |
| 暂停即冻结 | `timeScale = 0` | 暂停时敌人/AI/动画全部停止计算 |

**已知的待优化点**

| 问题 | 位置 | 建议 |
|---|---|---|
| 每次 Get/Release 都 `GetComponentsInChildren<IPoolable>` | `ObjectPool.cs` | 按实例缓存 `IPoolable[]` |
| 每帧 `state.IsName("Attack1")` 字符串比较 | `PlayerCombat.IsInComboWindow` | 用 `shortNameHash` 或直接读 `_comboIndex` |
| 每帧移动静态 Collider | `EnemyBrain.MoveTowardsTarget` | 上 `NavMeshAgent`（自带避障与物理处理） |
| 每次按键 `OverlapSphere` 分配 | `PlayerInteract` | `NonAlloc` + LayerMask |

<!-- TODO: 补一张 Profiler 截图，对比"池化前 / 池化后"的 GC Alloc，这是最省力的性能证据 -->

---

## 12. 面试讲解索引

| 主题 | 代码位置 | 一句话讲法 |
|---|---|---|
| 接口隔离 | `IHealthSystem` / `IDamageable` | 「读血和造成伤害拆成两个契约，UI 永远拿不到 `TakeDamage`」 |
| 模板方法 | `HealthComponent.ModifyIncomingDamage` / `HandleDeath` | 「护盾、无敌帧、死亡掉落都靠重写，父类一行不改」 |
| 依赖倒置 | `PlayerLifecycleBridge` / `AttackHitDetection` | 「只认 `IHealthSystem` / `IDamageable`，换实现零改动」 |
| 状态唯一真相源 | `GameState` + `GameManager.ApplyState` | 「全局流程一个枚举，副作用只在一处产生」 |
| UI 与逻辑解耦 | `UIManager` / `GamePanel` | 「面板声明自己在哪个状态显示，由总控仲裁，结构上不可能堆叠」 |
| 观察者模式 | 全项目 `event` | 「跨模块通信只发事件，不轮询」 |
| 状态机设计取舍 | `EnemyBrain` | 「4 个状态用 enum+switch 最直观，膨胀了才拆状态类」 |
| 动画驱动逻辑 | `AttackStateBehaviour` / `EndStateBehaviour` | 「播完没有由状态机保证，代码只管意图和取消窗口」 |
| 手感设计 | 连击窗口 0.4 + 输入缓冲 0.15 | 「提前按不丢、窗口内可取消」 |
| 对象池 | `ObjectPool` / `IPoolable` | 「池化对象没有第二次 `Start`，状态复位必须走协议」 |
| 数值外置 | 4 份 `ScriptableObject` | 「策划改数值不碰代码，多敌人共享同一份血量配置」 |
| 缓存友好 | `NonAlloc` / 平方距离 / `StringToHash` | 「物理查询和动画参数都不产生每帧分配」 |
| 边界条件 | `DamageCalculator.IsValidDamage` | 「NaN、Infinity、负数、0 全部拒绝，负数会变成治疗」 |
| 帧率无关插值 | `HealthBarUI.Damp` = `1 - e^(-k·dt)` | 「不同帧率下手感一致，比 `Lerp(a,b,0.1f)` 正确」 |
| 暂停的正确处置 | `GameManager` + `GamePanel` | 「`timeScale=0` 时协程和 `deltaTime` 全停，UI 必须用 unscaled」 |

**可能被追问，先准备答案**

1. 为什么用单例？三个 `FindFirstObjectByType` 的代价是什么？什么时候该换 DI / Service Locator？
2. `Time.timeScale = 0` 有哪些副作用？（协程 `WaitForSeconds` 冻结、`deltaTime = 0`、物理暂停）
3. 敌人为什么不用 NavMeshAgent？换成 NavMesh 后 FSM 要怎么改？（`SetDestination` + `remainingDistance`）
4. 如果让你重做，哪里会不一样？（asmdef/namespace、减少单例、AI 分层）
5. 怎么验证伤害公式是对的？（→ 这正是单元测试要补的原因）

<!-- TODO: 补一段 60 秒演示视频链接 + 可执行包链接 -->
<!-- TODO: 补上你负责的部分 / 项目背景 / 开发周期 -->

---

## 13. 第三方资源

| 资源 | 用途 | 来源 |
|---|---|---|
| Ellen（URP 预设） | 渲染管线设置 | <!-- TODO: 补 Asset Store 链接 --> |
| Simple Nature Pack | 场景植被与地形道具 | <!-- TODO: 补 Asset Store 链接 --> |
| TextMesh Pro + 中文字体 | UI 文本 | Unity 官方 |
| 角色模型与动画 | 玩家角色与 Idle/Walk/Run/Attack | <!-- TODO: 补来源（Mixamo 等） --> |

> 自研代码全部位于 `Assets/Main/`，`Assets/ThirdParty/` 仅为美术与管线预设，不含业务逻辑。

---

## 14. 联系

<!-- TODO: 填你的名字 / 邮箱 / GitHub / 作品集链接 -->

- 作者：`TODO`
- 邮箱：`TODO`
- 仓库：`https://github.com/mucheng777/RPG`
