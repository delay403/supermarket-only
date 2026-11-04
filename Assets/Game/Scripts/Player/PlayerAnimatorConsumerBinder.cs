using UnityEngine;
using Game.Glide;
using Game.Tower;

namespace Game.Player
{
    /// <summary>
    /// 换装后把跳跃 / 攀爬 / 滑翔控制器上的 Animator 引用指向新角色模型上的 <see cref="PlayerAnimatorDriver"/>。
    /// </summary>
    public static class PlayerAnimatorConsumerBinder
    {
        public static void RebindConsumers(GameObject playerRoot, PlayerAnimatorDriver driver)
        {
            if (playerRoot == null) return;

            foreach (var j in playerRoot.GetComponentsInChildren<PlayerJumpController>(true))
                j.BindAnimatorDriver(driver);

            foreach (var g in playerRoot.GetComponentsInChildren<GlideController>(true))
                g.BindAnimatorDriver(driver);

            foreach (var c in playerRoot.GetComponentsInChildren<ClimbController>(true))
                c.BindAnimatorDriver(driver);
        }
    }
}
