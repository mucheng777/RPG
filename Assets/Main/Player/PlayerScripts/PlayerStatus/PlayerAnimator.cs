using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    private Animator _animator;
    private PlayerLocomotion _locomotion;

    // Hash 缓存
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _locomotion = GetComponent<PlayerLocomotion>();
    }

    // ===== 由PlayerController调用 =====
    public void UpdateAnimParams()
    {
        if (_animator == null || _locomotion == null) return;

        // Speed → 驱动 Locomotion Blend Tree
        _animator.SetFloat(SpeedHash, _locomotion.GetCurrentSpeed(), 0.15f, Time.deltaTime);
    }
    // ===== 由PlayerController调用 =====
}