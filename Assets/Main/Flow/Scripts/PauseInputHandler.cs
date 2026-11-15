using UnityEngine;

/// <summary>
/// 暂停输入处理器：把"按键"翻译成 GameManager 的暂停意图。
/// 建议挂在 GameManager 所在物体上（一个物体承载"管理器 + 输入"两个小职责）。
///
/// 说明：
///   - 按键读取自 GameFlowProfile.pauseKey（默认 ESC），保证与配置单一真相源；
///   - 使用旧版 Input，与本项目既有的 CursorLockManager / HealthTestInput 保持一致
///     （Project Settings 的 Active Input Handling = Both，新旧输入可共存）。
///
/// ⚠ 与既有 CursorLockManager 的冲突（重要）：
///   CursorLockManager 用 ESC 释放光标，并且会在"点击鼠标左键"时把光标重新锁定。
///   暂停面板恰好要靠点击按钮操作，会被它反复抢锁。接入本系统后，
///   光标统一由 GameManager 接管，建议在场景中禁用/移除 CursorLockManager 组件。
/// </summary>
[AddComponentMenu("RPG/GameFlow/Pause Input")]
public class PauseInputHandler : MonoBehaviour
{
    [Tooltip("兜底按键；当场景中找不到 GameManager/其 Profile 时使用（默认 ESC）")]
    [SerializeField] private KeyCode fallbackPauseKey = KeyCode.Escape;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        KeyCode key = gm.Profile != null ? gm.Profile.pauseKey : fallbackPauseKey;
        if (Input.GetKeyDown(key)) gm.TogglePause();
    }
}
