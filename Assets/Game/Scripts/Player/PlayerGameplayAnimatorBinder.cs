using Game.Glide;
using Game.Tower;

namespace Game.Player
{
    /// <summary>
    /// 更换角色外观预制体后，把挂在玩家根上的控制器里的 <see cref="PlayerAnimatorDriver"/> 引用指到新模型。
    /// </summary>
    public static class PlayerGameplayAnimatorBinder
    {
        public static void BindAfterCharacterSpawn(PlayerRig rig)
        {
            if (rig == null || rig.characterSocket == null) return;

            var driver = rig.characterSocket.GetComponentInChildren<PlayerAnimatorDriver>();
            if (driver != null)
            {
                var root = rig.transform.root;
                driver.PrepareAfterRuntimeSpawn(root);

                foreach (var jc in root.GetComponentsInChildren<PlayerJumpController>(true))
                    jc.BindAnimatorDriver(driver);

                foreach (var cc in root.GetComponentsInChildren<ClimbController>(true))
                    cc.BindAnimatorDriver(driver);

                foreach (var gc in root.GetComponentsInChildren<GlideController>(true))
                    gc.BindAnimatorDriver(driver);
            }

            CharacterHierarchySockets.RebindAnimatorOnCharacterSocket(rig.characterSocket);
        }
    }
}
