# 斜面判定（Slope Detection）独立模块 — 实施计划

## Context（背景与目标）

当前项目角色移动（`PlayerLocomotion`，CharacterController 方案）的接地判定仅依赖 `_controller.isGrounded`，没有任何坡度感知能力。未来做斜面移动、下滑、跳跃修正等功能都需要坡度数据作为前提。

本任务目标：**在不修改任何现有脚本的前提下**，新增一个高内聚低耦合的斜面判定模块，对外只暴露只读数据接口（`IsOnSlope` / `SlopeAngle` / `GroundNormal` 等），内部实现完全封闭；并为未来切换移动方案（CC↔Rigidbody）、多角色复用、不同地面类型预留扩展点。

## 架构设计

### 类图（文字版）

```
ISlopeDataProvider (接口，数据契约)
├── 属性：IsGrounded / IsOnSlope / IsSlopeWalkable / SlopeAngle / GroundNormal / GroundPoint / CurrentSurfaceId
└── 事件：OnSlopeStateChanged(SlopeSnapshot)          ← 与 PlayerLocomotion 现有事件风格一致

SlopeProfile : ScriptableObject (配置)
├── maxSlopeAngle=45、minSlopeAngle=10、groundMask、castDistance=0.35、castRadiusFactor=0.85、refineNormal=true
└── List<GroundTypeEntry> groundTypes（layer→surfaceId 映射，为冰面/泥地等预留，v1 只存数据不做行为）

SlopeDetector : MonoBehaviour, ISlopeDataProvider (核心，唯一实现)
├── [DefaultExecutionOrder(-10)] 保证先于 PlayerController（order 0）执行
├── 内部私有：ResolveProbe()（CC 存在→读胶囊参数；无 CC→用 Profile 回退参数，RB 未来只加分支）
├── 内部私有：检测→精修法线→算坡角→滞回+确认帧防抖→缓存 SlopeSnapshot
├── OnDrawGizmosSelected：投射路径/hit 十字/法线箭头（绿=平地 黄=可走斜坡 红=超限）/下坡方向/坡角标签
└── [ContextMenu] RunDetectionNow()：单次检测打印快照

SlopeSnapshot (readonly struct)：某一帧的完整斜面状态快照
SlopeDebugProbe : MonoBehaviour (可选调试组件，订阅事件 + OnGUI 显示，用完可删)
```

### 职责划分

| 类                  | 职责         | 明确不做的事                         |
| ------------------ | ---------- | ------------------------------ |
| SlopeProfile       | 只存配置数据     | 无逻辑                            |
| SlopeDetector      | 只做检测、缓存、通知 | 不移动角色、不改 PlayerLocomotion 任何状态 |
| ISlopeDataProvider | 只定义数据契约    | —                              |
| SlopeDebugProbe    | 只显示调试信息    | 不参与游戏逻辑                        |

### 数据流（无侵入如何达成）

```
每帧：
SlopeDetector.Update (order -10)  →  检测并缓存 Snapshot，状态变化时发 OnSlopeStateChanged
PlayerController.Update (order 0) →  现有逻辑一行不动
未来消费方（新增文件，如 SlopeMovementAdapter）→ GetComponent<ISlopeDataProvider>() 只读读取，
   或订阅 OnSlopeStateChanged 事件 → 再驱动 PlayerLocomotion 的公开接口
```

* **接入点 1（数据拉取）**：任何新脚本都可通过 `GetComponent<ISlopeDataProvider>()` 只读消费，无需改动现有代码。

* **接入点 2（事件推送）**：坡度状态变化时主动通知，消费方无需轮询。

* **唯一场景操作**：在 Inspector 中给 Player 物体挂 `SlopeDetector` 组件并创建一个 `SlopeProfile` 资产——这是场景配置，不是代码修改。

### 扩展性设计

* **移动方案切换（CC↔RB）**：SlopeDetector 只依赖 Transform 检测，不依赖 CharacterController；胶囊参数通过 `ResolveProbe()` 内部解析（有 CC 读胶囊，无 CC 用 Profile 回退），未来 RB 只需在 `ResolveProbe` 加一个分支，接口与消费方零改动。

* **多角色复用**：组件即插即用，每个角色挂一个 SlopeDetector，各自引用不同/相同的 SlopeProfile 资产。

* **不同地面类型**：SlopeProfile 的 `groundTypes` 按 Layer 映射 `surfaceId`，接口暴露 `CurrentSurfaceId`；冰面/泥地的行为差异（滑动、摩擦）未来由消费方按 surfaceId 查表实现，检测器保持纯数据。

## 文件清单（全部为新增，不触碰任何现有文件）

```
Assets/Main/Player/PlayerScripts/SlopeDetection/
├── ISlopeDataProvider.cs   # 接口 + SlopeSnapshot 结构体
├── SlopeProfile.cs         # [CreateAssetMenu] 配置 SO
├── SlopeDetector.cs        # 核心组件（含 Gizmos 调试、ContextMenu）
└── SlopeDebugProbe.cs      # 可选调试组件（OnGUI 显示快照）
```

## 关键接口签名（实现目标）

```csharp
public interface ISlopeDataProvider
{
    bool IsGrounded { get; }          // 射线语义的接地判定（与 CC.isGrounded 可能短暂不一致，注释中标明）
    bool IsOnSlope { get; }           // 接地且坡角 ≥ minSlopeAngle（含滞回）
    bool IsSlopeWalkable { get; }     // 坡角 ≤ maxSlopeAngle
    float SlopeAngle { get; }         // 度
    Vector3 GroundNormal { get; }
    Vector3 GroundPoint { get; }
    int CurrentSurfaceId { get; }     // 地面类型 id，默认 0
    event System.Action<SlopeSnapshot> OnSlopeStateChanged;
}

public readonly struct SlopeSnapshot
{
    public bool IsGrounded, IsOnSlope, IsWalkable;
    public float SlopeAngle;
    public Vector3 GroundNormal, GroundPoint;
    public int SurfaceId;
}
```

防抖采用双重机制：角度滞回（进入/离开斜坡、walkable 阈值两侧各 ±5°）+ 连续确认帧数（2 帧），避免斜面边缘状态抖动刷事件。

## 实现步骤

1. 创建 `ISlopeDataProvider.cs`：接口 + SlopeSnapshot 只读结构体。
2. 创建 `SlopeProfile.cs`：ScriptableObject，含必须字段与 groundTypes 预留列表。
3. 创建 `SlopeDetector.cs`：核心检测（内部实现封闭）、执行顺序特性、事件、Gizmos、ContextMenu。
4. 创建 `SlopeDebugProbe.cs`：可选调试组件（`AddComponentMenu` 标注，便于打包时排除）。
5. Unity 中挂载：Player 物体 AddComponent `SlopeDetector`，创建 `SlopeProfile` 资产并赋值（此步留给用户在编辑器操作，或说明操作步骤）。

## 验证方式（不动现有代码即可闭环）

1. **编译**：Unity Console 无编译错误。
2. **Gizmos 目视**：选中 Player，Scene 视图查看投射路径与法线箭头颜色。
3. **测试场**：场景中摆三个旋转 15°/30°/50° 的 Cube（用户手动操作），角色走上去观察：

   * SlopeDebugProbe 的 OnGUI 显示 IsOnSlope/SlopeAngle 随坡度正确变化；

   * 30° 报 IsWalkable=true、50° 报 false；

   * 进入/离开斜坡时事件只触发一次（防抖生效）。
4. **现有功能回归**：移动/相机/动画行为与改动前一致（现有脚本零改动，风险为零）。

