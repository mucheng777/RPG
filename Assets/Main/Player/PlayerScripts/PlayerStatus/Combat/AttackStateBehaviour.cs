using UnityEngine;

/// <summary>
/// 挂在 Attack1 / Attack2 状态上，负责把"攻击段结束"的时机通知给 PlayerCombat。
///
/// 为什么用 SMB 而不是 Update 轮询 normalizedTime：
///   - Animator 的状态退出时机由状态机自己保证，OnStateExit 一定在过渡发生时回调
///   - 避免代码在 Attack→Empty→End 的中间帧误判
/// </summary>
public class AttackStateBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var combat = animator.GetComponentInParent<PlayerCombat>();
        if (combat == null) return;

        // 进入攻击段：通知当前是第几段
        int stage = stateInfo.IsName("Attack2") ? 2 : 1;
        combat.NotifyAttackEnter(stage);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var combat = animator.GetComponentInParent<PlayerCombat>();
        if (combat == null) return;

        // 攻击段退出 → 状态机会自动过渡到对应的 End（Animator 配置了无条件 ExitTime=1）
        combat.NotifyAttackExit();
    }
}
