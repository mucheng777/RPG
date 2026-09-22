//调试层
using UnityEngine;

/// <summary>
/// 斜面判定调试组件（可选，纯观察者）：订阅 ISlopeDataProvider，
/// OnGUI 实时显示当前快照与最近一次状态变化事件。
/// 仅用于开发期验证，验证完毕可直接移除，不参与任何游戏逻辑。
/// </summary>
[AddComponentMenu("RPG/Slope Debug Probe (调试用)")]
public class SlopeDebugProbe : MonoBehaviour
{
    private ISlopeDataProvider _provider;
    private string _lastEvent = "（暂无）";

    private void OnEnable()
    {
        _provider = GetComponent<ISlopeDataProvider>();
        if (_provider == null) _provider = GetComponentInParent<ISlopeDataProvider>();
        if (_provider != null)
            _provider.OnSlopeStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (_provider != null)
            _provider.OnSlopeStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(SlopeSnapshot snapshot)
    {
        _lastEvent = $"[{Time.time:F2}s] {snapshot}";
    }

    private void OnGUI()
    {
        const float w = 360f, h = 100f;
        GUI.Box(new Rect(12f, 12f, w, h), "Slope Detection Debug");

        if (_provider == null)
        {
            GUI.Label(new Rect(24f, 38f, w - 24f, 50f),
                "未找到 ISlopeDataProvider\n（请与 SlopeDetector 挂同一物体）");
            return;
        }

        GUI.Label(new Rect(24f, 38f, w - 24f, 60f),
            $"Grounded:{_provider.IsGrounded}  OnSlope:{_provider.IsOnSlope}  Walkable:{_provider.IsSlopeWalkable}\n" +
            $"Angle:{_provider.SlopeAngle:F1}  Surface:{_provider.CurrentSurfaceId}\n" +
            $"LastEvent: {_lastEvent}");
    }
}
