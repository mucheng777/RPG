using UnityEngine;

/// <summary>
/// 挂在 Attack1 ~ Attack4 状态上，负责把"攻击段进出"时机通知给 PlayerCombat。
///
/// Animator Controller 已删除 AttackXEnd 收刀中间态，
/// AttackN 播完 ExitTime=1 要么连到 Attack(N+1)，要么回 Empty。
/// </summary>
public class AttackStateBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var combat = animator.GetComponentInParent<PlayerCombat>();
        if (combat == null) return;

        int stage = ParseStage(stateInfo);
        if (stage > 0) combat.NotifyAttackEnter(stage);
    }

    private static int ParseStage(AnimatorStateInfo info)
    {
        if (info.IsName("Attack1")) return 1;
        if (info.IsName("Attack2")) return 2;
        if (info.IsName("Attack3")) return 3;
        if (info.IsName("Attack4")) return 4;
        return 0;
    }
}
