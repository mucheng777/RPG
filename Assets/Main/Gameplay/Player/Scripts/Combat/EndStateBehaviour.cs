using UnityEngine;

/// <summary>
/// 挂在 Attack1End / Attack2End 状态上，负责把"收刀结束"的时机通知给 PlayerCombat。
/// </summary>
public class EndStateBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var combat = animator.GetComponentInParent<PlayerCombat>();
        if (combat == null) return;

        combat.NotifyEndEnter();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var combat = animator.GetComponentInParent<PlayerCombat>();
        if (combat == null) return;

        // 收刀退出 → 回到 Empty（可能是播完，也可能是 MoveTrigger 中断）
        combat.NotifyEndExit();
    }
}
