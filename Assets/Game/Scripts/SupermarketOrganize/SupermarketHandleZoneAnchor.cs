using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>将 HandleZone 挂到第一人称摄像机前，使手持物品随视角旋转。</summary>
    public static class SupermarketHandleZoneAnchor
    {
        public static SupermarketHandleZone EnsureOnCamera(Transform player, Camera camera, SupermarketOrganizeConfig config)
        {
            if (camera == null)
                return null;

            config ??= SupermarketOrganizeConfig.ResolveRuntime();

            var handle = player != null
                ? player.GetComponentInChildren<SupermarketHandleZone>(true)
                : null;
            handle ??= Object.FindObjectOfType<SupermarketHandleZone>();

            if (handle == null)
            {
                var handleGo = new GameObject("HandleZone");
                handle = handleGo.AddComponent<SupermarketHandleZone>();
            }

            handle.transform.SetParent(camera.transform, false);
            handle.transform.localRotation = Quaternion.identity;
            if (config != null)
                handle.transform.localPosition = config.handleZoneLocalOffset;

            handle.ApplyConfig(config);
            return handle;
        }
    }
}
