using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 动物医院可交互物体使用的 Layer 约定：AI / Item / WorldInteractable。
    /// </summary>
    public static class AnimalHospitalInteractableLayers
    {
        public const string AiLayerName = "AI";
        public const string ItemLayerName = "Item";
        public const string WorldInteractableLayerName = "WorldInteractable";

        public static int Ai => LayerMask.NameToLayer(AiLayerName);
        public static int Item => LayerMask.NameToLayer(ItemLayerName);
        public static int WorldInteractable => LayerMask.NameToLayer(WorldInteractableLayerName);

        public static LayerMask DefaultRaycastMask =>
            LayerMask.GetMask(AiLayerName, ItemLayerName, WorldInteractableLayerName);

        public static void ApplyLayerRecursive(GameObject root, int layer)
        {
            if (root == null || layer < 0)
                return;

            root.layer = layer;
            for (var i = 0; i < root.transform.childCount; i++)
                ApplyLayerRecursive(root.transform.GetChild(i).gameObject, layer);
        }

        public static void ApplyAiLayer(GameObject root) => ApplyLayerRecursive(root, Ai);

        public static void ApplyItemLayer(GameObject root) => ApplyLayerRecursive(root, Item);

        public static void ApplyWorldInteractableLayer(GameObject root) =>
            ApplyLayerRecursive(root, WorldInteractable);
    }
}
