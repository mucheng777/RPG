# RPG · 第三人称动作 RPG 技术演示

> 一个以「**可扩展的模块化框架**」为目标、而不是「堆功能」的 Unity 3D 动作 RPG 技术 Demo。
> 每个系统都按「**接口契约 → 通用实现 → 具体子类 → 数据资产**」四层拆分，设计理由写在代码注释里，本 README 负责把它们串起来。

**当前状态**

| 层 | 进度 |
|---|---|
| 框架层（生命值 / 战斗 / 流程 / UI / 对象池 / 相机 / 背包） | ✅ 完成 |
| 内容层（双敌人 AI + 动画 / 掉落 / 拾取 / 打击感） | ✅ 完成 |
| 待补（音频 / 存档 / 技能 / 单元测试 / 打包发布） | 🚧 进行中 |

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
| 动画 | Animator + StateMachineBehaviour + Animation Event，`Apply Root Motion` 由 `RootMotionController` 按剪辑分拣接管 |
| 物理 | `CharacterController`（玩家） / `NavMeshAgent`（敌人寻路，携带实心 Collider 阻挡） |
| 导航 | AI Navigation（NavMesh 烘焙，`EnemyBrain` 只发 `SetDestination` / `isStopped` / `Warp`） |
| 启动场景 | `Assets/Scenes/01-GameScene.unity`（Build Settings 中唯一场景） |
| 目标平台 | PC / Windows <!-- TODO: 补上实际出包验证过的平台 --> |
| 游戏循环 | 主菜单 → 游玩 → 死亡 → 复活/重开 → 返回主菜单，闭环可玩 |
| 手写代码 | 55 个脚本 / 约 4700 行（`PlayerControls.cs` 1507 行为 Input System 自动生成，不计入） |

---

## 2. 快速开始

### 打开与运行

1. Unity Hub 用 **2022.3.62f3c1** 打开本项目；
2. 打开 `Assets/Scenes/01-GameScene.unity`；
3. 点 Play，点主菜单的「开始游戏」。

> 首次打开会编译 TextMesh Pro / URP 资产，属正常现象。
> 若敌人站着不动，检查场景是否已烘焙 NavMesh（`Window → AI → Navigation → Bake`）。

### 操作说明

| 输入 | 行为 | 输入绑定 |
|---|---|---|
| `WASD` / 方向键 | 八向移动 | `PlayerControls.Player.Move` |
| `Left Shift` | 奔跑 | `Sprint` |
| 鼠标移动 | 第三人称视角 | `Look` |
| 鼠标左键 | 攻击 / **四段连击**（带软锁定） | `Fire` |
| `Tab` / `I` | 打开 / 关闭背包 | `InventoryUI.toggleKey` |
| `E` | 拾取脚下最近的掉落物 | `PickupPromptUI.pickupKey` |
| `Esc` | 暂停 / 继续 | `GameFlowProfile.pauseKey` |
| `T` | 对自己造成 20 真实伤害（测试用） | `HealthTestInput` |

> `HealthTestInput` 整体包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 里，正式发布包不参与编译。

---

## 3. 目录结构

```
Assets/
├── Main/                                    # 全部自研代码，按业务域划分
│   ├── Core/                                # 与具体玩法无关的基础设施
│   │   ├── Health/
│   │   │   ├── Scripts/
│   │   │   │   ├── IHealthSystem.cs         # 只读状态 + 事件 + 治疗（UI/技能用）
│   │   │   │   ├── IDamageable.cs           # 受伤入口（攻击方用）
│   │   │   │   ├── DamageInfo.cs            # readonly struct 伤害包 + DamageType
│   │   │   │   ├── DamageCalculator.cs      # 纯静态伤害公式（无状态，可单测）
│   │   │   │   ├── HealthProfile.cs         # ScriptableObject 配置
│   │   │   │   └── HealthComponent.cs       # 通用实现（模板方法模式）
│   │   │   ├── UI/HealthBarUI.cs            # 血条：前景条 + 残影条双值插值
│   │   │   ├── Data/HealthProfile.asset
│   │   │   └── Prefabs/HealthBarCanvas.prefab
│   │   └── ObjectPool/
│   │       └── Scripts/                     # IPoolable / ObjectPool<T> / PoolManager
│   │
│   ├── Flow/                                # 游戏流程（主菜单/游玩/暂停/死亡）
│   │   ├── Scripts/                         # GameState / GameManager / GameFlowProfile
│   │   │                                    # PlayerLifecycleBridge / PauseInputHandler / IRevivable
│   │   ├── UI/                              # UIManager + GamePanel 基类 + 3 个流程面板
│   │   └── Data/GameFlowProfile.asset
│   │
│   ├── Gameplay/
│   │   ├── Player/
│   │   │   ├── Input/PlayerControls.cs       # .inputactions 生成的 C# 包装（勿手改）
│   │   │   ├── Scripts/
│   │   │   │   ├── Controller/               # PlayerController / PlayerLocomotion
│   │   │   │   │                             # PlayerAnimator / RootMotionController
│   │   │   │   ├── Combat/                   # PlayerCombat（四段连击+软锁定）
│   │   │   │   │                             # AttackStateBehaviour（SMB）
│   │   │   │   │                             # AttackHitDetection（命中+顿帧+震屏）
│   │   │   │   ├── Camera/ThirdPersonCamera.cs
│   │   │   │   └── Health/PlayerHealth.cs    # 无敌帧 + 复活
│   │   │   ├── Animations/{Clips,Controllers}/Ellen
│   │   │   ├── Models / Materials / Textures
│   │   │   ├── Weapons/Ellen/                # Pistol / Staff 模型、材质、贴图
│   │   │   └── Prefabs/Ellen.prefab
│   │   │
│   │   ├── Enemy/
│   │   │   ├── Scripts/
│   │   │   │   ├── EnemyBrain.cs             # 四状态 FSM（Idle/Chase/Attack/Dead）+ NavMesh
│   │   │   │   ├── EnemyHealth.cs            # 继承 HealthComponent，接入对象池
│   │   │   │   ├── EnemyAnimator.cs          # 推 Speed / 攻击 / 受击 / 死亡参数
│   │   │   │   ├── EnemyMeleeAttack.cs       # 近战：动画事件 AttackBegin 结算伤害
│   │   │   │   ├── EnemyRangedAttack.cs      # 远程：动画事件 Shoot 发射炸弹
│   │   │   │   ├── EnemyProjectile.cs        # 炸弹：SphereCast 连续检测
│   │   │   │   ├── EnemyHitFeedback.cs       # 闪白 + 小击退
│   │   │   │   ├── EnemyProfile.cs
│   │   │   │   └── EnemySpawner.cs           # 演示用刷怪（调用 PoolManager）
│   │   │   ├── Animations/{Clips,Controllers}  # Chomper / Spitter
│   │   │   ├── Data/                          # EnemyProfile / SpitterProfile
│   │   │   ├── Models / Materials / Textures
│   │   │   └── Prefabs/Chomper.prefab / Spitter.prefab
│   │   │
│   │   ├── Inventory/
│   │   │   ├── Scripts/                      # Inventory / InventorySlot / ItemDefinition
│   │   │   │                                 # ItemPickup / LootTable / LootSpawner
│   │   │   │                                 # EnemyLootDropper / ItemDragContext / InteractionCursorGate
│   │   │   ├── UI/                           # InventoryUI / InventorySlotView / PickupPromptUI
│   │   │   ├── Data/                          # 5 个 ItemDefinition + 2 张 LootTable
│   │   │   └── Prefabs/Solt.prefab
│   │   │
│   │   └── Environment/                      # 场景美术材质
│   │
│   └── Tests/PlayerLoseHPTestScript/HealthTestInput.cs  # 调试自伤按键（条件编译剔除）
│
├── Scenes/01-GameScene.unity
├── ThirdParty/                               # Ellen(URP 预设) / SimpleNaturePack(场景美术)
├── TextMesh Pro/ · Fonts/                    # TMP 资源与中文字体
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
│   InventoryUI ──只调 Inventory 公开 API──▶ 数据层              │
│      ▲                    │                                   │
│      │ 注册 / 受控        │ 面板只调用「意图方法」              │
│   GamePanel(MainMenu / Pause / Death)                         │
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
│   PlayerCombat ◀──SMB 回调── AttackStateBehaviour             │
│   AttackHitDetection → IDamageable（顿帧 + 震屏）              │
│   EnemyBrain（FSM）→ NavMeshAgent                             │
│      ├─ AttackExecuted 事件 → EnemyMeleeAttack / EnemyRangedAttack │
│      └─ Animation Event（AttackBegin / Shoot）→ 真正结算       │
│   Inventory（纯数据）◀── InventoryUI（纯表现）                  │
│   ObjectPool<T> ← PoolManager ← EnemySpawner / LootSpawner    │
└──────────────────────────────────────────────────────────────┘
```

### 4.2 五条贯穿全项目的原则

1. **逻辑层不认识 UI** —— `GameManager` 只广播事件，不知道面板存在；`HealthComponent` 只发 `OnHealthChanged`，不引用 `HealthBarUI`；`Inventory` 不认识 `InventoryUI`。
2. **跨模块只认接口** —— `IDamageable` / `IHealthSystem` / `IRevivable` / `IPoolable`，新增目标类型零改动。
3. **数值外置** —— 所有可调参数进 `ScriptableObject`（`HealthProfile` / `EnemyProfile` / `SpitterProfile` / `GameFlowProfile`），逻辑类只保留兜底默认值。
4. **事件驱动，不轮询** —— 跨模块通信一律 `event`，禁止每帧读别人的状态字段。
5. **攻击判定交给动画事件** —— 「什么时候算打中」由动画剪辑的 `Animation Event` 决定，代码只负责「打中了做什么」。

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
- **玩家专属** `PlayerHealth`：`invincibleDuration = 0.5s` 受伤无敌帧，`reviveInvincibility = 1.5s` 复活无敌；`HandleDeath()` 重写为空，保留对象以便原地复活。
- **池化兼容**：`HealthComponent.Start()` 显式广播一次初始血量；池化复用不会重跑 `Start`，因此 `EnemyHealth.ResetState()` 手动 `NotifyHealthChanged()`。

**血条** `HealthBarUI` 用三条值做表现：`targetFill`（逻辑真值）/ `frontFill`（红条，指数阻尼追赶）/ `ghostFill`（白色残影，受伤后延迟再追），全部按 `1 - e^(-k·dt)` 做帧率无关插值；治疗时残影直接同步，不反向拖尾。敌人副本上挂 `HealthBarCanvas`（World Space），`HealthProfile.showHealthBar / worldSpaceUI` 已生效。

### 5.2 战斗 Combat（四段连击）

**职责**：四段连击的状态推进与命中判定，**动画状态机负责「时机」，代码只负责「意图」**。

| 文件 | 职责 |
|---|---|
| `PlayerCombat.cs` | 连击阶段机（`Idle / Attacking`）、`comboWindowStart`、输入缓冲、软锁定 |
| `AttackStateBehaviour.cs` | 挂在 `Attack1 ~ Attack4` 上的 SMB，`OnStateEnter` 通知当前段数 |
| `AttackHitDetection.cs` | 按动画 `normalizedTime` 窗口做一次 `OverlapSphere`，命中后触发顿帧 + 震屏 |

**Animator 结构**（`Ellen` 控制器）

```
Base Layer   : Locomotion（BlendTree：Idle / Walk / Run）
Attack Layer : Empty → Attack1 → Attack2 → Attack3 → Attack4 → Empty
                        （每段 ExitTime=1 自动过渡，无 AttackXEnd 中间态）
参数          : Speed(float) AttackTrigger(trigger)
```

**状态维护方式**

- `PlayerCombat.UpdateAttack()` **每帧读 Animator 当前状态**：只要不在 `Attack1~4` 就认为攻击结束（有缓冲则立刻起手，否则回 `Idle`）。不再依赖 SMB + 协程链路，规避「Animator 过渡配错一点就卡死」。
- SMB 只做一件事：`NotifyAttackEnter(stage)` 更新 `_comboIndex`（当前第几段）。相位的切换统一由 `UpdateAttack` 维护，避免双真相源。
- 刚出招的 `0.15s` 内 Animator 还没切进 Attack 状态（`SetTrigger` 下一帧生效），此窗口内不当成「攻击结束」，否则移动输入不归零会一路平移、连击也推不动。

**手感参数**

| 参数 | 默认值 | 含义 |
|---|---|---|
| `comboWindowStart` | 0.75 | 当前段播到 75% 后才允许推进下一段 |
| `inputBufferTime` | 0.15s | 窗口外按攻击会被缓存，攻击结束后立即起手 |
| 一段/二段/三段/四段伤害 | 20 / 30 / 40 / 55 | `AttackHitDetection` |
| 命中窗口 | 0.35 ~ 0.65 | 每段只判定一次（`_hitDone` 锁 + 段切换复位） |
| 命中球 | 半径 0.8，偏移 (0, 1, 0.9) | 只打 Layer 7（Enemy） |
| 顿帧 | `timeScale=0.05`，持续 0.06s（真实时间） | 命中至少一个活目标才触发 |
| 震屏 | 0.15s，幅度 0.12m，线性衰减 | `ThirdPersonCamera.Shake` |

**软锁定**：出手瞬间在 `lockRadius=4m`、前方 180° 扇形内找最近的**存活**敌人（跳过 `EnemyBrain.IsDead`），直接把角色转向它。`OverlapSphereNonAlloc` 复用 8 格缓冲。

**Root Motion 分拣**：`RootMotionController.OnAnimatorMove()` 只在当前状态是 `Attack1~4` 时执行 `_controller.Move(delta)`；其余状态（走路/待机/受击）由 `PlayerLocomotion` 接管位移。配置层面：需要自带位移的剪辑（攻击/翻滚）不勾 `Bake Into Pose`，纯代码驱动的剪辑勾上。

### 5.3 敌人 AI（NavMesh + 可插拔攻击）

**职责**：四状态轻量 FSM，移动完全交给 `NavMeshAgent`。

```
Idle  ──目标进入 chaseRange──▶ Chase
Chase ──进入 attackRange─────▶ Attack（进入时立即出手一次）
Chase ──超出 loseTargetRange─▶ Idle（脱战）
Attack──超出 attackRange×1.2─▶ Chase（滞回，防边界抖动）
任意  ──OnDied───────────────▶ Dead
```

- **Brain 与 Agent 只有三个接触点**：`SetDestination`（去哪）、`isStopped`（走/停）、`Warp`（池复用同步位置）。路径、贴地、爬坡、怪与怪避障全由 Agent 负责，不再手写 `transform.position +=`（两套位移并存会互相拉扯导致抖动穿模）。
- **移动权限单一真相源**：`SetState` 里统一 `_agent.isStopped = next != Chase`、`updateRotation = next == Chase`；非 Chase 状态 `ResetPath()` 清掉残路径。
- **索敌**：`Physics.OverlapSphereNonAlloc` + `LayerMask`（只找 Layer 6 Player），静态缓冲区，零 GC。
- **距离口径**：全部用**水平平方距离**（剔除 Y 轴、免开方）。敌人可能半埋做视觉对齐，三维距离会让 Y 差吃掉整个 `attackRange`。
- **攻击协议**：`AttackOnce()` 只抛 `AttackExecuted` 事件，**不结算伤害**。伤害方式由身上挂的组件决定：
  - `EnemyMeleeAttack`：收到事件只记目标，动画播到咬合帧（`Chomper_Attack.anim` 的 `AttackBegin`，0.572s）才 `TakeDamage`，并做 `attackRange × 1.3` 挥空判定。
  - `EnemyRangedAttack`：收到事件只记目标，动画播到吐弹帧（`Spitter_Spit.anim` 的 `Shoot`，0.8s）才 `PoolManager.Spawn` 一颗炸弹。
  - 因此 `EnemyBrain` 被近战/远程怪原样复用，新增攻击方式 = 加一个组件 + 在剪辑里放一个同名事件。
- **攻击霸体**：`EnemyAnimator.OnDamaged` 里 `if (_brain.IsAttacking) return;`，攻击期间不插播受击动画（远程怪前摇 0.8s，否则贴身时每刀都打断，永远吐不出弹）。闪白与击退仍由 `EnemyHitFeedback` 负责。
- **受击反馈** `EnemyHitFeedback`：`MaterialPropertyBlock` 改 `_BaseColor` 闪白 0.08s（不污染共享材质），`NavMeshAgent.Move` 沿反方向小击退 0.35m / 0.12s（用未缩放时间，顿帧时不拖慢）。

**形态**：`enum + switch` 单文件。当前 4 个状态、每个 ≤10 行，拆成状态类反而增加跳转成本；等状态带独立数据或数量膨胀时再重构。
**编辑器可视化**：`OnDrawGizmosSelected` 画追击/攻击范围圈。
**NavMesh 未命中保护**：`WarnIfNotOnNavMesh()` 在出生时打日志，省得对着「怪不动」发呆。

### 5.4 背包与掉落 Inventory

**职责**：纯数据容器 + 纯表现 UI + 数据驱动的掉落。

| 文件 | 职责 |
|---|---|
| `Inventory.cs` | 槽位、增删改查、容量、排序、跨容器 `Transfer`；`OnChanged` 是 UI 唯一刷新入口 |
| `InventorySlot.cs` / `ItemDefinition.cs` | 单槽数据 / 物品 SO（名称、类型、图标、`maxStack`、世界模型） |
| `InventoryUI.cs` | 打开/关闭（`Tab`/`I`）、拖拽移动/合并/交换、详情、排序；界面全在 Hierarchy 手搭 |
| `InventorySlotView.cs` | 单格表现，拖拽手柄 |
| `ItemPickup.cs` | 地面掉落物实体：浮动/自转/超时、静态注册表、`TryCollect` |
| `PickupPromptUI.cs` | 找离玩家最近的掉落物并显示「按 E 拾取」 |
| `LootTable.cs` | 掉落表 SO：条目 `{物品, 数量区间, 权重}`，纯随机 `Roll()` |
| `LootSpawner.cs` / `EnemyLootDropper.cs` | 生成地面实体（贴地/散落/池化） / 订阅敌人死亡按表掉落 |
| `InteractionCursorGate.cs` | 背包/拾取时临时接管光标与相机输入（引用计数，多 UI 共存） |

**数据与表现分离**：`Inventory` 不引用任何 UI；`InventoryUI` 只调用 `TryAdd / RemoveAt / PlaceInto / Transfer / Sort` 这些带返回值的公开 API。

**交互规则**：目标为空 → 直接放入；目标同类 → 合并到上限，放不下留在原位；目标不同类 → 仅整堆交换（不做拆分，保持容器语义）。

**打开背包时的耦合控制**（不改 `GameManager` / 相机 / `PlayerController`）：
1. `InteractionCursorGate` 以 `DefaultExecutionOrder(900)` 在 `GameManager(-100)` 锁完光标之后解锁；
2. 临时禁用相机自己的 `PlayerControls` 实例（只停视角，不停玩家）；
3. `Time.timeScale = 0` 冻结世界，禁用 `PlayerController` 防止点 UI 触发攻击；
4. 关闭/暂停/死亡时自动恢复，**先锁光标、再激活相机输入**，过滤掉背包期间的鼠标位移脏数据。

**掉落权重**：相对比例，只比大小；权重 0 表示永不掉落。`EnemyLootDropper` 严格 `OnEnable/OnDisable` 配对订阅，池复用不会重复掉落，一命只掉一次。

### 5.5 游戏流程 GameFlow

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

### 5.6 UI 层

**职责**：按流程状态**仲裁**面板显隐，结构上杜绝「面板堆叠/残留」。

| 文件 | 职责 |
|---|---|
| `UIManager.cs` | 面板注册表 + 状态变化统一刷新 + 同状态重复声明告警 |
| `GamePanel.cs` | 抽象基类：声明 `ShownState`、CanvasGroup 淡入淡出、图片接入适配点 |
| `MainMenuPanel / PausePanel / DeathPanel` | 三个具体面板，只把按钮翻译成 `GameManager` 调用 |

1. 面板在 `OnEnable` 自动向 `UIManager` 注册（无需手动拖引用）；
2. `UIManager` 订阅 `GameManager.OnStateChanged`，状态一变即 `RefreshAll()`；
3. 只有 `ShownState == 当前状态` 的面板显示，**其余全部隐藏**（互斥，无残留）；
4. 注册时两面板声明同一状态会立即 `LogWarning`，把配置错误消灭在启动期。

> 面板必须常驻激活，显隐只由 CanvasGroup 的 `alpha / interactable / blocksRaycasts` 控制；过渡用 `unscaledDeltaTime`（暂停/死亡时 `timeScale = 0`）。

### 5.7 对象池

**职责**：消除高频 `Instantiate/Destroy` 带来的 GC 与卡顿。

```
PoolManager (MonoBehaviour, 全局唯一)
   └─ Dictionary<GameObject prefab, ObjectPool<Transform>>
         └─ ObjectPool<T> (纯 C#)：Stack<T> 存空闲实例
               Get():  空栈则 Instantiate → 置位 → SetActive(true) → 逐个 IPoolable.OnSpawned()
               Release(): 逐个 OnDespawned() → SetActive(false) → 挂回池根 → 入栈
```

- **顺序很关键**：`SetActive(true)` 先触发子组件 `OnEnable`（事件订阅就位），之后才调 `OnSpawned()`，这样 `OnSpawned` 里广播的血量不会被还没订阅的 UI 丢掉。
- **池复用关键坑**：池只改了 `Transform`，`NavMeshAgent` 内部还记着上一世位置，下一帧会把怪拉回原地。`EnemyBrain.OnSpawned()` 用 `_agent.Warp(transform.position)` 把 Agent 内部位置同步并吸附到最近 NavMesh 点。
- **`IPoolable` 协议**：`OnSpawned` 等价「第二次 Awake」，必须把对象恢复到刚实例化的状态；`OnDespawned` 清理运行期外部引用；两者必须幂等。
- **兜底**：`Despawn` 收到非池来源（场景手摆对象）时退化为 `Destroy`。
- 当前已接入：`Chomper / Spitter`（`EnemyHealth` + `EnemyBrain`）、`EnemyProjectile`、`ItemPickup`。

### 5.8 移动与相机

**玩家移动** `PlayerLocomotion`（极简版）

- 唯一驱动入口 `UpdateLocomotion()`，由 `PlayerController.Update` 每帧调用。
- 移动方向以相机 Yaw 为基准；`Mathf.SmoothDampAngle` 平滑转向。
- 重力累积，`isGrounded` 时钳到 `-2f` 保持接地吸附。
- 攻击期间 `PlayerController` 把 Move 归零（`MoveIntent` 保留真实意图），角色原地出招。

**第三人称相机** `ThirdPersonCamera`（自研弹簧臂）

- `LateUpdate` 执行，避免与角色移动打架；俯仰钳制 -25°~60°，`SmoothDampAngle` 平滑。
- **弹簧臂碰撞**：从目标点向相机方向 `Raycast`，命中则收缩距离（`hit.distance - 0.3`，不低于 `minDistance`）。
- **防御性 NaN 过滤**：输入系统在场景重载时序下可能产出 NaN/Infinity，污染 `SmoothDamp` 的 velocity 后会永久传播，因此读输入、写 transform 前后都做 `IsValid` 校验，并支持状态自愈。
- **暂停保护**：`if (Time.deltaTime <= 0f) return;`。`timeScale=0` 时 `SmoothDamp` 内部会 0/0 得 NaN，不做保护则每次暂停再恢复视角都会跳到固定角度。
- **命中震屏**：`Shake(duration, magnitude)` 在最终位置上叠加 `Random.insideUnitSphere`，用 `unscaledDeltaTime` 衰减，顿帧期间也能抖。

### 5.9 输入

- `PlayerControls.cs` 由 `.inputactions` 生成，暴露 `Player/Move`、`Look`、`Sprint`、`Fire`；
- `PlayerController` 每帧把输入翻译成 `Locomotion.SetMoveInput / SetSprint`，攻击时把 Move 归零；
- 点击检测前先判断 `EventSystem.IsPointerOverGameObject()`，指针在 UI 上时不触发攻击（否则点「开始游戏」会顺带挥刀）；
- `InventoryUI` / `PickupPromptUI` / `PauseInputHandler` / `HealthTestInput` 仍用旧 `Input.GetKeyDown`，靠 `Active Input Handling = Both` 兼容。

---

## 6. 关键调用链

### 6.1 一次普攻（四段连击）

```
按下左键
 └─ PlayerController.Update
     ├─ IsPointerOverUI()? 是 → 忽略（防点 UI 挥刀）
     └─ PlayerCombat.TryAttack()
         ├─ FaceNearestEnemy()                 // 软锁定转向最近存活敌人
         ├─ Idle      → StartAttack(1)         → SetTrigger("AttackTrigger")
         ├─ Attacking → 窗口内(≥75%) 且 <4段 → AdvanceCombo()
         │              否则缓存输入 0.15s
         ↓ Animator 过渡到 Attack1...Attack4（Attack Layer）
         AttackStateBehaviour.OnStateEnter → PlayerCombat.NotifyAttackEnter(stage)
         ↓ 动画播到 0.35 ~ 0.65
         AttackHitDetection.Update
             └─ PerformHit(stage)  (OverlapSphere, Layer 7, 每段一次)
                 ├─ 跳过已死目标 / 自己
                 └─ IDamageable.TakeDamage(DamageInfo)
                     └─ HealthComponent：校验 → 护甲修正 → 扣血
                         → OnDamaged → OnHealthChanged（血条）→ 死亡判定
             └─ hitAny → HitStop()（timeScale 0.05 / 0.06s）
                       + ThirdPersonCamera.Shake(0.15s, 0.12m)
         ↓ ExitTime = 1
         PlayerCombat.UpdateAttack() 每帧轮询：不在 Attack1~4 → EndAttack / 缓冲起手
```

### 6.2 敌人近战咬合（动画事件驱动）

```
EnemyBrain.TickAttack → _attackTimer 到点 → AttackExecuted
 └─ EnemyMeleeAttack.HandleAttackStart()    // 只记录目标，不结算
 ↓ Animator 播 Attack
 Chomper_Attack.anim 0.572s → Animation Event "AttackBegin"
 └─ EnemyMeleeAttack.AttackBegin()
     ├─ 前摇期间目标跑出 attackRange×1.3 → 挥空
     └─ IDamageable.TakeDamage(...)
 ↓ EnemyHealth.OnDamaged
     ├─ EnemyAnimator：攻击中忽略受击动画（霸体）
     └─ EnemyHitFeedback：闪白 0.08s + 击退 0.35m
```

### 6.3 敌人远程吐弹（动画事件驱动）

```
EnemyBrain.AttackExecuted
 └─ EnemyRangedAttack.HandleAttackStart()   // 只记录目标
 ↓ Animator 播 Spitter_Spit
 Spitter_Spit.anim 0.8s → Animation Event "Shoot"
 └─ EnemyRangedAttack.Shoot()
     ├─ TransformPoint(spawnOffset)  → 跟随朝向的嘴部位置
     ├─ PoolManager.Spawn(bomb, pos, LookRotation(dir))
     └─ EnemyProjectile.Launch(dir, speed, damage, source)
 ↓ 每帧 SphereCastNonAlloc 扫本帧路径
     ├─ 撞 Player → TakeDamage → 回池
     └─ 撞地面/墙 → 只回池不扣血
```

### 6.4 敌人死亡 → 掉落 → 回池

```
HealthComponent.TakeDamage → 归零 → OnDied
 ├─ EnemyBrain.HandleDied → State.Dead（停一切行为）
 ├─ EnemyLootDropper → LootTable.Roll() → LootSpawner 贴地生成 LootTable.dropPrefab
 └─ EnemyHealth.HandleDeath() → Invoke(ReturnToPool, destroyDelay)
     └─ PoolManager.Despawn → IPoolable.OnDespawned → SetActive(false) → 入栈

下次 EnemySpawner.SpawnWave()
 └─ PoolManager.Spawn → SetActive(true)（OnEnable 重订阅）
     ├─ EnemyHealth.OnSpawned → ResetState（满血 + 手动广播血条）
     └─ EnemyBrain.OnSpawned  → Idle + Agent.Warp + ResetPath
```

### 6.5 玩家死亡 → 复活

```
HealthComponent.TakeDamage → 血量归零
 └─ _isAlive = false → OnDied
     ├─ PlayerHealth.HandleDeath()（空实现，保留对象）
     └─ PlayerLifecycleBridge → GameManager.NotifyPlayerDied()
         └─ SetState(PlayerDead)
             ├─ ApplyState: timeScale=0 / 音频暂停 / 释放光标 / 启动复活计时
             └─ OnStateChanged → UIManager.RefreshAll() → DeathPanel 淡入

点击「复活」
 └─ GameManager.RespawnPlayer()
     ├─ PlayerLifecycleBridge.RevivePlayer()
     │   ├─ IRevivable.Revive()  // 满血 + 解锁 + 1.5s 无敌 + 广播血条
     │   └─ Teleport()           // 先禁用 CC → 赋值 → 再启用
     └─ SetState(Playing) → timeScale=1 → 面板互斥隐藏
```

---

## 7. 设计决策与取舍

| # | 决策 | 理由 | 代价 / 何时该改 |
|---|---|---|---|
| 1 | 伤害只有一个入口 `TakeDamage(DamageInfo)` | 无法绕过校验/事件链直接改血量 | 单一职责压在一个方法上，逻辑膨胀时要拆管线 |
| 2 | `IHealthSystem` 与 `IDamageable` 拆开 | 接口隔离：UI/技能只读，拿不到伤害能力 | 接口变多，新人要理解契约分工 |
| 3 | `ModifyIncomingDamage` / `HandleDeath` 设 `protected virtual` | 开闭原则：护盾/无敌/掉落都靠重写 | 继承层次深了会变成「父类地狱」，届时改组合 |
| 4 | 攻击判定改由 **Animation Event** 驱动 | 判定帧与动画帧严格对齐，不再「动画没播到就掉血」 | 换攻击动画必须补同名事件，否则打不中人 |
| 5 | 连击相位用 `UpdateAttack` 每帧轮询 Animator | 不依赖 SMB + 协程链路，过渡配错也不会卡死 | 每帧一次 `GetCurrentAnimatorStateInfo`，开销可忽略 |
| 6 | 连击窗口 0.75 + 0.15s 输入缓冲 | 手感：提前按不丢、后段不被瞬点跳过 | 缓冲时长是经验值，需真机调 |
| 7 | 出手软锁定最近存活敌人 | 不需要手动对准，动作游戏常规手感 | 扇形/半径是经验值；多目标战斗需扩展选择策略 |
| 8 | 顿帧用 `Time.timeScale` + `WaitForSecondsRealtime` | 最省事的全局顿帧，且不会被自身拖慢 | 顿帧期间所有 `deltaTime` 归零，UI/相机必须用 unscaled |
| 9 | 敌人移动交给 `NavMeshAgent` | 避障、贴地、爬坡、怪间分散全部免费 | 需烘焙 NavMesh；动态障碍要 `NavMeshObstacle` |
| 10 | `EnemyBrain` 只抛 `AttackExecuted`，不结算伤害 | 近战/远程/未来法术都能复用同一套 FSM | 攻击方式多了要约束「谁订阅谁负责」 |
| 11 | 攻击组件可插拔（近战/远程各一个） | 新增怪物 = 换配置 + 挂组件，不复制脚本 | 组件间职责边界要清晰 |
| 12 | 攻击霸体（攻击中不播受击动画） | 前摇 0.8s 的远程怪否则永远被打断 | Boss 需要「可打断」时要做韧性/霸体分级 |
| 13 | 全局流程收敛为单个 `GameState` 枚举 | 杜绝 `isPaused/isDead/isMenuOpen` 组合不一致 | 新增状态要同时改枚举 + `ApplyState` |
| 14 | 副作用集中在 `ApplyState()` | 状态变化只有一处产生副作用，可审计 | 所有环境依赖耦合在一个方法里 |
| 15 | 面板「声明自己在哪显示」，由 `UIManager` 仲裁 | 结构上不可能出现两个全屏面板 | 面板不能自己控制显隐（刻意的限制） |
| 16 | 对象池 + `IPoolable` 协议 | 避免高频 Instantiate/Destroy 的 GC 峰值 | 每个池化类型都要正确实现 `OnSpawned/OnDespawned` |
| 17 | 背包数据（`Inventory`）与表现（`InventoryUI`）分离 | 换 UI 不动数据，换容器不动 UI | 跨容器操作需要 `Transfer` 这类静态方法兜底 |
| 18 | 背包打开用「外部仲裁」接管光标/相机 | 不修改既有 `GameManager`/相机，零侵入 | 依赖执行顺序（`DefaultExecutionOrder`），要写清楚 |
| 19 | 所有数值进 ScriptableObject | 策划改参数不碰代码、不改预制体 | 资产多了要管理；运行时改会污染资产 |
| 20 | 敌人 FSM 用 `enum + switch` 不拆类 | 4 个状态、每个 ≤10 行，跳转成本最低 | 状态 >6 或要带独立数据时必须重构 |
| 21 | 距离判定全用平方距离 + 水平投影 | 省开方；剔除 Y 差避免「贴身但进不了攻击态」 | 飞行/高低差 AI 需另开高度判定 |
| 22 | 主菜单与游戏在**同一场景**（`timeScale = 0` 冻结） | 无需场景切换开销，重开 = 重载场景 | 场景变大后加载慢；正式项目应拆场景 + Addressables |

---

## 8. 数据配置一览

| 资产 | 路径 | 关键字段（默认值） |
|---|---|---|
| `HealthProfile` | `Assets/Main/Core/Health/Data/HealthProfile.asset` | `maxHealth` 100 · `destroyDelay` 2 · `showHealthBar` · `worldSpaceUI`（`armor` 在 `HealthComponent` 组件上，100 点 = 50% 减伤） |
| `EnemyProfile` | `Assets/Main/Gameplay/Enemy/Data/EnemyProfile.asset` | `moveSpeed` 2 · `attackDamage` 10 · `attackInterval` 2 · `attackRange` 2.5 · `chaseRange` 8 · `loseTargetRange` 12 |
| `SpitterProfile` | `Assets/Main/Gameplay/Enemy/Data/SpitterProfile.asset` | `moveSpeed` 2 · `attackDamage` 10 · `attackInterval` 2 · `attackRange` 8 · `chaseRange` 12 · `loseTargetRange` 16 |
| `GameFlowProfile` | `Assets/Main/Flow/Data/GameFlowProfile.asset` | `startState` MainMenu · `pauseKey` Escape · `pauseAudioListener` · `lockCursorWhilePlaying` · `autoRespawnDelay` 0 · `restartReloadsScene` true |
| `Item_*`（5 份） | `Assets/Main/Gameplay/Inventory/Data/` | 名称 · 类型 · 图标 · `maxStack` · 世界模型 |
| `ChomperLootTable` / `SpitterLootTable` | `Assets/Main/Gameplay/Inventory/Data/` | 条目 `{物品, min~max 数量, 权重}` |

**Layer 约定**

| Layer | 名称 | 用途 |
|---|---|---|
| 3 | `Ground` | 地面/墙体（炸弹碰到即消失） |
| 6 | `Player` | 玩家根物体；`EnemyBrain.targetMask`、`EnemyProjectile.damageMask` 只认这一层 |
| 7 | `Enemy` | 敌人；`AttackHitDetection.hitMask`、`PlayerCombat.enemyMask` 只认这一层 |

> **Layer Collision Matrix** 里 `Player × Enemy` 必须勾选：玩家是 `CharacterController`，敌人是实心 `CapsuleCollider`，保留碰撞两边都不穿模。

> 创建新资产：Project 右键 → `Create → RPG → Health / Enemy / Game Flow`。

---

## 9. 扩展指南

### 新增一种敌人（近战或远程）

1. 复制 `Chomper.prefab` 或 `Spitter.prefab`；
2. 新建 `EnemyProfile` 资产填数值（或共享已有的），拖到 `EnemyBrain.profile`；
3. 近战挂 `EnemyMeleeAttack`，远程挂 `EnemyRangedAttack` —— **不要改 `EnemyBrain`**；
4. 若有新攻击动画，**必须在剪辑里补同名 Animation Event**（近战 `AttackBegin` / 远程 `Shoot`），否则不会真的打中人；
5. 拖到 `EnemySpawner.enemyPrefab`，并确认场景已烘焙 NavMesh。

### 新增一个全局流程状态（如过场/结算）

1. `GameState` 追加枚举值；
2. `GameManager.ApplyState()` 补一条副作用规则。注意当前 `bool playing = state == Playing` 同时决定 `timeScale` / 音频 / 光标，新状态要先想清楚它算不算「playing」；
3. 新建面板继承 `GamePanel` 并重写 `ShownState` → `GameManager` / `UIManager` **零改动**。

### 新增一个 UI 面板

```csharp
public class SettingsPanel : GamePanel
{
    public override GameState ShownState => GameState.Paused;  // ⚠️ 与 PausePanel 冲突会告警
}
```

### 新增一种可池化对象（伤害数字 / 特效 / 技能弹道）

1. 调 `PoolManager.Instance.Spawn(prefab, pos, rot)` 生成；
2. 组件实现 `IPoolable`：`OnSpawned()` 复位状态、`OnDespawned()` 清理引用；
3. 归还调 `PoolManager.Instance.Despawn(go)`。`PoolManager` 与 `ObjectPool` 零改动。

### 新增伤害类型 / 抗性 / 暴击

- 抗性：在 `DamageCalculator.Calculate` 加 `multiplier` 参数或按 `DamageType` 查表；
- 暴击：调用方算好后填进 `DamageInfo.Amount`，受击方不需要知道；
- 护盾：`PlayerHealth` / `EnemyHealth` 重写 `ModifyIncomingDamage()`，先扣盾再 `base.`。

### 新增一种攻击手段（如法术）

1. 写一个订阅 `EnemyBrain.AttackExecuted`（或玩家侧 `PlayerCombat`）的组件；
2. 收到事件只记目标，真正的结算放到对应动画的 `Animation Event` 里；
3. 复用 `IDamageable` 与 `PoolManager`，`EnemyBrain` / `HealthComponent` 零改动。

---

## 10. 已知限制与 TODO

> 面试时主动讲出来，比被问出来好。逐条修掉后就从这份列表里删除。

### 待补（按优先级）

- [ ] **零音效**：全项目无 `AudioSource`/`AudioClip`。缺挥砍、命中、受伤、死亡、UI 点击 + AudioMixer。这是性价比最高的补齐项。
- [ ] **无单元测试**：`test-framework 1.1.33` 已安装，但 `Assets` 下 `[Test]` 出现 0 次。`DamageCalculator` / `HealthComponent` / `ObjectPool` / `Inventory.Transfer` 都是现成的 EditMode 用例目标，且需先加 `Tests` asmdef。
- [ ] **无存档 / 读档**：`Inventory` 底部已留 `CaptureState / RestoreState` 扩展点注释，缺 `ItemDatabase`（id → 资产映射）与 JSON 版本迁移。
- [ ] **无技能与冷却、无资源条、无经验升级**。
- [ ] **无 asmdef、无 namespace**：55 个类全在 `Assembly-CSharp` 全局命名空间。加 asmdef 能讲「编译隔离 / 增量编译 / 防循环依赖」。
- [ ] **无 HUD**：没有击杀数、目标提示、技能冷却、伤害数字飘字。
- [ ] **未出包**：没有 Windows 构建包、演示视频、Profiler 截图；README 首屏 GIF 仍是 TODO。
- [ ] **`Solt.prefab` 拼写错误**（应为 `Slot.prefab`）。
- [ ] **`EnemyMeleeAttack.debugLog` 默认 true**，持续刷 Console，打包前应关闭。
- [ ] **新旧输入混用**：`InventoryUI` / `PickupPromptUI` / `PauseInputHandler` / `HealthTestInput` 用旧 `Input`，靠 `Both` 模式兼容。
- [ ] **Spitter 无受击动画**：`IsHurt` 触发器可能切到空状态，需补剪辑或在 `EnemyAnimator` 里跳过。
- [ ] **攻击霸体是全局开关**，没有韧性/分级。Boss 战需要「可打断」时要扩展。

### 工程规范

- [ ] Git 历史较碎，建议整理提交信息、补 `.gitattributes` / LFS（fbx、png 直接进 Git）
- [ ] `companyName` 仍是 `DefaultCompany`
- [ ] 无 `Profiler` 数据/截图，性能优化缺少证据
- [ ] `.trae/documents/slope-detection-module.md` 是已删除的斜面模块旧文档，可清理

---

## 11. 性能说明

**已做的**

| 手段 | 位置 | 效果 |
|---|---|---|
| 对象池复用敌人 / 炸弹 / 掉落物 | `PoolManager` / `ObjectPool<T>` | 消除高频 Instantiate/Destroy 的 GC 峰值 |
| 物理查询 `NonAlloc` | `EnemyBrain` / `AttackHitDetection` / `EnemyProjectile` / `PlayerCombat` | 索敌、命中、弹道、软锁定全部零分配 |
| 平方距离替代 `Distance` | `EnemyBrain` / `PlayerCombat` / `EnemyMeleeAttack` | 每帧省开方 |
| 参数哈希替代字符串 | `PlayerCombat` / `PlayerAnimator` / `EnemyAnimator` 的 `Animator.StringToHash` | 避免每帧字符串查表 |
| 事件替代轮询 | 全局 | 无每帧 `GetComponent` / 状态扫描 |
| 闪白用 MaterialPropertyBlock | `EnemyHitFeedback` | 不产生材质实例，避免材质泄漏 |
| 暂停即冻结 | `timeScale = 0` | 暂停时敌人/AI/动画全部停止计算 |

**已知的待优化点**

| 问题 | 位置 | 建议 |
|---|---|---|
| 每帧 `state.IsName("AttackN")` 字符串比较 | `PlayerCombat` / `AttackHitDetection` / `EnemyAnimator` | 用 `shortNameHash` 比较 |
| 每次 Get/Release 都 `GetComponentsInChildren<IPoolable>` | `ObjectPool.cs` | 按实例缓存 `IPoolable[]` |
| `HealthBarUI` 每帧插值 | 敌人数量多时 | 改为仅在 `OnHealthChanged` 期间更新 |

<!-- TODO: 补一张 Profiler 截图，对比「池化前 / 池化后」的 GC Alloc，这是最省力的性能证据 -->

---

## 12. 面试讲解索引

| 主题 | 代码位置 | 一句话讲法 |
|---|---|---|
| 接口隔离 | `IHealthSystem` / `IDamageable` | 「读血和造成伤害拆成两个契约，UI 永远拿不到 `TakeDamage`」 |
| 模板方法 | `HealthComponent.ModifyIncomingDamage` / `HandleDeath` | 「护盾、无敌帧、死亡掉落都靠重写，父类一行不改」 |
| 依赖倒置 | `PlayerLifecycleBridge` / `AttackHitDetection` | 「只认 `IHealthSystem` / `IDamageable`，换实现零改动」 |
| 动画事件驱动判定 | `EnemyMeleeAttack.AttackBegin` / `EnemyRangedAttack.Shoot` | 「判定帧写在 `.anim` 的 Animation Event 上，动画播到哪就打到哪」 |
| 可插拔攻击 | `EnemyBrain.AttackExecuted` → 近战/远程组件 | 「Brain 只负责出手，伤害方式换成组件，新增攻击手段不改 AI」 |
| 双真相源规避 | `PlayerCombat.UpdateAttack` 每帧轮询 | 「相位只看 Animator 实际状态，SMB 只更新段数，不会因为过渡配错卡死」 |
| 状态唯一真相源 | `GameState` + `GameManager.ApplyState` | 「全局流程一个枚举，副作用只在一处产生」 |
| UI 与逻辑解耦 | `UIManager` / `GamePanel` / `Inventory` | 「数据层不认识 UI，面板声明自己在哪个状态显示」 |
| 观察者模式 | 全项目 `event` | 「跨模块通信只发事件，不轮询」 |
| 状态机设计取舍 | `EnemyBrain` | 「4 个状态用 enum+switch 最直观，膨胀了才拆状态类」 |
| NavMesh 分层 | `EnemyBrain` 只管三个接触点 | 「Brain 决定去哪/何时停，路径和避障交给 Agent，两套位移并存会抖」 |
| 对象池 | `ObjectPool` / `IPoolable` | 「池化对象没有第二次 `Start`，状态复位必须走协议」 |
| 池复用 Agent 坑 | `EnemyBrain.OnSpawned` 的 `Warp` | 「池只改 Transform，Agent 内部位置要 Warp 同步，否则下一帧拉回原地」 |
| 打击感 | `AttackHitDetection` 顿帧 + `ThirdPersonCamera.Shake` | 「`timeScale` 顿帧必须配 `WaitForSecondsRealtime`，否则自己被拖慢」 |
| NaN 防御 | `ThirdPersonCamera` | 「`timeScale=0` 时 `SmoothDamp` 会 0/0 得 NaN 并永久传播，必须提前 return」 |
| 帧率无关插值 | `HealthBarUI.Damp` = `1 - e^(-k·dt)` | 「不同帧率下手感一致，比 `Lerp(a,b,0.1f)` 正确」 |
| 暂停的正确处置 | `GameManager` + `GamePanel` | 「`timeScale=0` 时协程和 `deltaTime` 全停，UI 必须用 unscaled」 |
| 零侵入改造 | `InteractionCursorGate` | 「不改进既有光标/相机代码，用外部仲裁 + 执行顺序接管」 |

**可能被追问，先准备答案**

1. 为什么用单例？`FindFirstObjectByType` 的代价是什么？什么时候该换 DI / Service Locator？
2. `Time.timeScale = 0` 有哪些副作用？（协程 `WaitForSeconds` 冻结、`deltaTime = 0`、物理暂停）
3. 动画事件和代码轮询 `normalizedTime` 各自的优劣？（精度 vs 配置成本）
4. 如果让你重做，哪里会不一样？（asmdef/namespace、减少单例、AI 分层、拆场景）
5. 怎么验证伤害公式 / 背包合并逻辑是对的？（→ 这正是单元测试要补的原因）

<!-- TODO: 补一段 60 秒演示视频链接 + 可执行包链接 -->
<!-- TODO: 补上你负责的部分 / 项目背景 / 开发周期 -->

---

## 13. 第三方资源

| 资源 | 用途 | 来源 |
|---|---|---|
| Ellen（URP 预设） | 玩家角色模型、动画与武器 | <!-- TODO: 补 Asset Store 链接 --> |
| Simple Nature Pack | 场景植被与地形道具 | <!-- TODO: 补 Asset Store 链接 --> |
| TextMesh Pro + 中文字体 | UI 文本 | Unity 官方 |
| Chomper / Spitter 模型与动画 | 近战 / 远程敌人 | <!-- TODO: 补来源 --> |

> 自研代码全部位于 `Assets/Main/`，`Assets/ThirdParty/` 与 `Assets/Main/Gameplay/*/Models` 仅为美术资源，不含业务逻辑。

---

## 14. 联系

<!-- TODO: 填你的名字 / 邮箱 / GitHub / 作品集链接 -->

- 作者：`TODO`
- 邮箱：`TODO`
- 仓库：`https://github.com/mucheng777/RPG`
