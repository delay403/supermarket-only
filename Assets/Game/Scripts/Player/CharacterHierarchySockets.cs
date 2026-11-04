using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 角色预制体内骨骼挂点名约定：<see cref="BindWingSocketUnderCharacter"/> 会把 <see cref="PlayerRig.wingSocket"/> 指到新实例下的同名 Transform。
    /// </summary>
    public static class CharacterHierarchySockets
    {
        public const string WingRootTransformName = "WingRoot";
        public const string CarryRootTransformName = "CarryRoot";
        public const string BackRodRootTransformName = "BackRodRoot";
        public const string HandRodRootTransformName = "HandRodRoot";
        public const string FishRootTransformName = "FishRoot";
        public const string HandFishRootTransformName = "HandFishRoot";
        public const string FootRootTransformName = "FootRoot";

        /// <summary>旧名兼容：等同 <see cref="BackRodRootTransformName"/>。</summary>
        public const string RodRootTransformName = BackRodRootTransformName;

        /// <summary>
        /// 绑定背部 <see cref="BackRodRootTransformName"/> 与手部 <see cref="HandRodRootTransformName"/> 挂点。
        /// </summary>
        public static void BindRodSocketsUnderCharacter(PlayerRig rig, Transform characterRoot)
        {
            if (rig == null || characterRoot == null) return;

            var back = FindDeepChildByName(characterRoot, BackRodRootTransformName)
                       ?? FindDeepChildByName(characterRoot, "RodRoot");
            var hand = FindDeepChildByName(characterRoot, HandRodRootTransformName);

            if (back != null)
            {
                rig.rodBackSocket = back;
                rig.rodSocket = back;
            }

            if (hand != null)
                rig.rodHandSocket = hand;
        }

        /// <summary>钓竿挂在角色骨骼/挂点下时使用；找不到则保留 <paramref name="rig"/> 上场景里配置的 rodSocket。</summary>
        public static void BindRodSocketUnderCharacter(PlayerRig rig, Transform characterRoot)
        {
            BindRodSocketsUnderCharacter(rig, characterRoot);
        }

        /// <summary>背部展示鱼挂点；找不到节点则保留 <paramref name="rig"/> 上原有赋值。</summary>
        public static void BindFishSocketUnderCharacter(PlayerRig rig, Transform characterRoot)
        {
            if (rig == null || characterRoot == null) return;
            var found = FindDeepChildByName(characterRoot, FishRootTransformName);
            if (found != null)
                rig.fishSocket = found;
        }

        /// <summary>手持钓获鱼展示挂点（HandFishRoot）。</summary>
        public static void BindHandFishSocketUnderCharacter(PlayerRig rig, Transform characterRoot)
        {
            if (rig == null || characterRoot == null) return;
            var found = FindDeepChildByName(characterRoot, HandFishRootTransformName);
            if (found != null)
                rig.handFishSocket = found;
        }

        /// <summary>脚下特效挂点（FootRoot）；找不到节点则保留 <paramref name="rig"/> 上原有赋值。</summary>
        public static void BindFootSocketUnderCharacter(PlayerRig rig, Transform characterRoot)
        {
            if (rig == null || characterRoot == null) return;
            var found = FindDeepChildByName(characterRoot, FootRootTransformName);
            if (found != null)
                rig.footSocket = found;
        }

        /// <summary>翅膀挂在角色骨骼下时使用；找不到节点则保留 <paramref name="rig"/> 上原有赋值。</summary>
        public static void BindWingSocketUnderCharacter(PlayerRig rig, Transform characterRoot)
        {
            if (rig == null || characterRoot == null) return;
            var found = FindDeepChildByName(characterRoot, WingRootTransformName);
            if (found != null)
                rig.wingSocket = found;
        }

        /// <summary>携带物挂点（CarryRoot）；找不到节点则保留 <paramref name="rig"/> 上原有赋值。</summary>
        public static void BindCarrySocketUnderCharacter(PlayerRig rig, Transform characterRoot)
        {
            if (rig == null || characterRoot == null) return;
            var found = FindDeepChildByName(characterRoot, CarryRootTransformName);
            if (found != null)
                rig.carrySocket = found;
        }

        /// <summary>在 <paramref name="root"/> 子层级内按名称查找 Transform（含 root 自身）。</summary>
        public static Transform FindDeepChildByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName)) return null;
            if (root.name == targetName)
                return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var hit = FindDeepChildByName(root.GetChild(i), targetName);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        /// <summary>
        /// Instantiate 默认命名为 <c>prefabName (Clone)</c>；挂在父节点上的 <see cref="Animator"/> 使用的 Avatar / 片段曲线按<strong>预制根物体名</strong>记录路径（如 <c>juese1/Bip001/...</c>）。
        /// 名称含 <c>(Clone)</c> 时绑定对不上，表现为子模型 T-Pose / 无动画。
        /// </summary>
        public static void AlignInstantiatedCharacterNameToPrefab(GameObject instance, GameObject prefabSource)
        {
            if (instance == null || prefabSource == null) return;
            instance.name = prefabSource.name;
        }

        /// <summary>
        /// 网格实例换好后，刷新挂在 <paramref name="characterSocket"/>（通常为 CharacterRoot）上的 Animator 与子层级的绑定。
        /// Humanoid 父节点 Animator + 运行时挂上子网格时，单次 Rebind 有时不足以把肌肉写入骨骼；编辑器里拖预制体常会隐式完成等价刷新。
        /// </summary>
        public static void RebindAnimatorOnCharacterSocket(Transform characterSocket)
        {
            if (characterSocket == null) return;
            var anim = characterSocket.GetComponent<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null) return;

            // Cull Update Transforms 在骨架尚未正确绑定时可能跳过骨骼更新，表现为状态机在跑、网格 T-Pose。
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            anim.Rebind();
            anim.Update(0f);
            anim.Update(0f);
        }

        /// <summary>
        /// 网格预制体内若有 Animator（少见），实例化后做一次 Rebind；无 Controller 的仍会 enable。
        /// </summary>
        public static void WarmupAnimatorsUnderInstance(GameObject root)
        {
            if (root == null) return;
            var animators = root.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                var a = animators[i];
                if (a == null) continue;
                a.enabled = true;
                if (a.runtimeAnimatorController == null) continue;
                a.Rebind();
                a.Update(0f);
            }
        }
    }
}
