using UnityEngine;

namespace Game.Player
{
    public sealed class PlayerRig : MonoBehaviour
    {
        [Header("Sockets")]
        public Transform petSocket;
        [Tooltip("第二只宠物；空则与 petSocket 共用（EquipmentSpawner 会做水平偏移）。")]
        public Transform petSocket1;
        [Tooltip("第三只宠物；空则与 petSocket 共用。")]
        public Transform petSocket2;

        [Tooltip("翅膀挂点；若骨骼在角色预制体内，EquipmentSpawner 会在换装后自动绑定名为 WingRoot 的子节点。")]
        public Transform wingSocket;

        [Tooltip("携带物挂点（CarryRoot）；换装后自动绑定。")]
        public Transform carrySocket;

        public Transform trailSocket;

        [Tooltip("背部收竿挂点（BackRodRoot）；换装后自动绑定。与 rodSocket 同步。")]
        public Transform rodBackSocket;

        [Tooltip("手持钓鱼挂点（HandRodRoot）；开始钓鱼时切换到此。")]
        public Transform rodHandSocket;

        [Tooltip("兼容旧字段：背部钓竿挂点；未绑 BackRodRoot 时可手动指定。")]
        public Transform rodSocket;

        [Tooltip("背部展示鱼挂点（FishRoot）；换装后自动绑定。")]
        public Transform fishSocket;

        [Tooltip("手持钓获鱼展示挂点（HandFishRoot）；钓获展示动画期间鱼挂在此。")]
        public Transform handFishSocket;

        [Tooltip("脚下特效挂点（FootRoot）；钓获庆祝粒子等。")]
        public Transform footSocket;

        [Tooltip("角色外观父节点（常与层级中 CharacterRoot 对应）；为空时在运行时自动查找名为 CharacterRoot 的子物体。")]
        public Transform characterSocket;

        /// <summary>
        /// 解析场景中挂载角色的节点（CharacterRoot）。已在 Inspector 赋值则不改。
        /// </summary>
        public void EnsureSceneSocketsResolved()
        {
            if (characterSocket == null)
            {
                Transform[] roots = { transform, transform.root };
                for (var i = 0; i < roots.Length; i++)
                {
                    var r = roots[i];
                    if (r == null) continue;
                    var hit = CharacterHierarchySockets.FindDeepChildByName(r, "CharacterRoot");
                    if (hit != null)
                    {
                        characterSocket = hit;
                        break;
                    }
                }
            }

            if (rodBackSocket == null)
            {
                var sceneRod = CharacterHierarchySockets.FindDeepChildByName(transform, "RodRoot")
                               ?? CharacterHierarchySockets.FindDeepChildByName(
                                   transform,
                                   CharacterHierarchySockets.BackRodRootTransformName);
                if (sceneRod != null)
                {
                    rodBackSocket = sceneRod;
                    if (rodSocket == null)
                        rodSocket = sceneRod;
                }
            }
        }

        public Transform GetPetSocket(int slotIndex)
        {
            return slotIndex switch
            {
                0 => petSocket,
                1 => petSocket1 != null ? petSocket1 : petSocket,
                2 => petSocket2 != null ? petSocket2 : petSocket,
                _ => petSocket
            };
        }

        /// <summary>与 petSocket 为同一节点时需对多只实例做 localPosition 偏移，避免重叠。</summary>
        public bool PetSlotUsesSharedPetRoot(int slotIndex)
        {
            var t = GetPetSocket(slotIndex);
            return t != null && petSocket != null && t == petSocket;
        }
    }
}
