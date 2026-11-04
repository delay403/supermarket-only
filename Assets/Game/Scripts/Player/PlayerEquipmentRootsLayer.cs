using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 将装备挂点（翅膀/拖尾/宠物根）下<strong>整棵 Transform 子树</strong>的所有 GameObject 设为 Player。
    /// 包含 Instantiate 出来的多级预制（例如 WingRoot → 翅膀(Clone) → chibang → Bone…）；需在预制挂上<strong>之后</strong>调用。
    /// </summary>
    public static class PlayerEquipmentRootsLayer
    {
        private static readonly string[] DefaultEquipmentRootNames =
        {
            "WingRoot",
            "CarryRoot",
            "TrailRoot",
            "Pet1Root",
            "Pet2Root",
            "Pet3Root",
            "FishRoot",
            "FootRoot",
        };

        public static void ApplyToHierarchy(Transform playerRoot)
        {
            if (playerRoot == null)
                return;

            var layer = LayerMask.NameToLayer("Player");
            if (layer < 0)
                return;

            var all = playerRoot.GetComponentsInChildren<Transform>(true);
            for (var n = 0; n < DefaultEquipmentRootNames.Length; n++)
            {
                var want = DefaultEquipmentRootNames[n];
                for (var i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t != null && t != playerRoot && t.name == want)
                    {
                        // 含根的每一名 Transform.gameObject，及其全部后代（克隆预制体内多级子物体）。
                        SetLayerRecursively(t.gameObject, layer);
                        break;
                    }
                }
            }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            var tr = go.transform;
            for (var i = 0; i < tr.childCount; i++)
                SetLayerRecursively(tr.GetChild(i).gameObject, layer);
        }
    }
}
