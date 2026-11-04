using System.Collections.Generic;
using Game.Core.Config;
using Game.DestroyCity.Config;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>燃料实例世界模型与碰撞体（从 <see cref="FuelDatabase"/> 读取）。</summary>
    public static class FuelInstanceVisualUtility
    {
        public static void ApplyLevelVisual(Transform host, ref GameObject visualInstance, int fuelLevel, int interactableLayer)
        {
            if (host == null)
                return;

            if (visualInstance != null)
                Object.Destroy(visualInstance);

            visualInstance = null;
            GameObject prefab = null;
            var db = ConfigRegistry.DestroyCityFuels;
            if (db != null && db.TryGetByLevel(fuelLevel, out var def) && def != null)
                prefab = def.worldPrefab;

            if (prefab != null)
            {
                visualInstance = Object.Instantiate(prefab, host);
                visualInstance.transform.localPosition = Vector3.zero;
                visualInstance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                visualInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visualInstance.transform.SetParent(host, false);
                visualInstance.transform.localScale = Vector3.one * 0.4f;
            }

            if (interactableLayer >= 0)
                SetLayerRecursive(visualInstance, interactableLayer);

            EnsurePickupCollider(host, interactableLayer);
        }

        /// <summary>玩家背上的燃料克隆体：隐藏世界 UI，禁用交互与碰撞。</summary>
        public static void ConfigureCarriedVisual(GameObject root)
        {
            if (root == null)
                return;

            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.gameObject.SetActive(false);

            foreach (var view in root.GetComponentsInChildren<FuelInstanceView>(true))
                view.enabled = false;

            foreach (var col in root.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }

        public static void EnsurePickupCollider(Transform host, int interactableLayer)
        {
            if (host == null)
                return;

            var col = host.GetComponent<Collider>();
            if (col == null)
            {
                col = host.gameObject.AddComponent<BoxCollider>();
                ((BoxCollider)col).size = Vector3.one * 0.5f;
            }

            if (interactableLayer >= 0)
                host.gameObject.layer = interactableLayer;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null)
                return;

            go.layer = layer;
            for (var i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
