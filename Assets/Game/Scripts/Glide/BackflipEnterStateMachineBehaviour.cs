using UnityEngine;

namespace Game.Glide
{
    /// <summary>
    /// 挂在 Animator「空翻」状态上：进入状态时通知 <see cref="GlideController"/> 开始脚本驱动的 +Z/+Y 位移，
    /// 持续时间与速度见滑翔控制器上的「空翻」参数。
    /// </summary>
    public sealed class BackflipEnterStateMachineBehaviour : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            var glide = animator.transform.root.GetComponentInChildren<GlideController>(true);
            if (glide != null)
                glide.BeginBackflipPhase();
        }
    }
}
