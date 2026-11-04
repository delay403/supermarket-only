using Game.DestroyCity.Config;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>摧毁城市经验飘字入口（实例化预制体）。</summary>
    public static class DestroyCityXpFloater
    {
        static DestroyCityXpFloatView _registeredPrefab;
        static bool _searchedSceneSettings;

        public static void RegisterPrefab(DestroyCityXpFloatView prefab)
        {
            if (prefab != null)
                _registeredPrefab = prefab;
        }

        public static void Show(Vector3 worldPosition, float xpAmount)
        {
            if (xpAmount <= 0.01f)
                return;

            var prefab = ResolvePrefab();
            if (prefab == null)
            {
                Debug.LogWarning(
                    "[DestroyCityXpFloater] 未配置飘字预制体。请在场景中添加 DestroyCityXpFloaterSettings，" +
                    $"或将预制体放到 Resources/{DestroyCityResourcePaths.XpFloatPrefab}.");
                return;
            }

            var inst = Object.Instantiate(prefab, worldPosition, Quaternion.identity);
            inst.Play(xpAmount);
        }

        static DestroyCityXpFloatView ResolvePrefab()
        {
            if (_registeredPrefab != null)
                return _registeredPrefab;

            if (!_searchedSceneSettings)
            {
                _searchedSceneSettings = true;
                var settings = Object.FindObjectOfType<DestroyCityXpFloaterSettings>();
                if (settings != null)
                    settings.ApplyToFloater();
            }

            if (_registeredPrefab != null)
                return _registeredPrefab;

            return Resources.Load<DestroyCityXpFloatView>(DestroyCityResourcePaths.XpFloatPrefab);
        }
    }
}
