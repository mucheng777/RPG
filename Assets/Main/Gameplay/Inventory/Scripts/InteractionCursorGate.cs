using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 交互光标门禁 —— 背包 / 拾取提示打开时，临时接管"光标显示 + 相机视角输入"。
///
/// 背景：项目里 GameManager 每帧在 LateUpdate 锁定光标（Playing 状态），
/// ThirdPersonCamera 又直接在 LateUpdate 读鼠标增量转视角。
/// 直接改这两个既有模块会破坏它们的职责边界，所以这里用"外部仲裁"的方式：
///   1. 执行顺序设得比 GameManager(-100) 更晚（900），在它锁完光标后再解锁；
///   2. 临时 Disable 相机自己的 PlayerControls 实例（只影响相机视角，不影响玩家移动）；
///   3. 多个 UI 同时请求用引用计数，最后一个释放时才恢复。
///
/// 这样既不修改既有代码，也不会出现"关背包后光标抢锁/视角乱转"。
/// </summary>
[DefaultExecutionOrder(900)]
[AddComponentMenu("RPG/Inventory/Interaction Cursor Gate")]
public class InteractionCursorGate : MonoBehaviour
{
    private static InteractionCursorGate _instance;

    /// <summary>惰性单例：首次使用时自动创建，无需手动摆场景物体</summary>
    public static InteractionCursorGate Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<InteractionCursorGate>();
                if (_instance == null)
                    _instance = new GameObject("[InteractionCursorGate]").AddComponent<InteractionCursorGate>();
            }
            return _instance;
        }
    }

    private readonly HashSet<Object> _owners = new HashSet<Object>();
    private ThirdPersonCamera _camera;
    private bool _cameraInputDisabled;

    public bool IsActive => _owners.Count > 0;

    public static void Acquire(Object owner)
    {
        if (owner != null) Instance.AcquireInternal(owner);
    }

    public static void Release(Object owner)
    {
        if (_instance != null && owner != null) _instance.ReleaseInternal(owner);
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void AcquireInternal(Object owner)
    {
        if (_owners.Add(owner)) ApplyUnlockedState();
    }

    private void ReleaseInternal(Object owner)
    {
        if (!_owners.Remove(owner)) return;
        if (_owners.Count == 0) RestoreGameplayState();
    }

    void LateUpdate()
    {
        // 清理被销毁的请求方（例如打开背包时玩家死亡、UI 被禁用）
        if (_owners.Count == 0) return;

        _owners.RemoveWhere(o => o == null);
        if (_owners.Count == 0)
        {
            RestoreGameplayState();
            return;
        }

        // GameManager 在本帧更早的 LateUpdate 里会重新锁光标，这里再解锁一次
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!_cameraInputDisabled) SetCameraInput(false);
    }

    private void ApplyUnlockedState()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetCameraInput(false);
    }

    private void RestoreGameplayState()
    {
        SetCameraInput(true);

        var gm = GameManager.Instance;
        bool shouldLock = gm != null && gm.IsPlaying && (gm.Profile == null || gm.Profile.lockCursorWhilePlaying);
        if (shouldLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SetCameraInput(bool enabled)
    {
        if (_camera == null) _camera = FindFirstObjectByType<ThirdPersonCamera>();
        if (_camera == null || _camera.input == null) return;

        if (enabled) _camera.input.Enable();
        else _camera.input.Disable();

        _cameraInputDisabled = !enabled;
    }
}
