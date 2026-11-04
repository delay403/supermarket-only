using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// 为玩家/AI 装配安全区身上标记（仅绑定已有子物体，不自动创建占位模型）。
    /// </summary>
    public static class WormDodgePresenceSetup
    {
        public static void EnsureSafeZoneIndicator(GameObject root)
        {
            if (root == null)
                return;

            var marker = FindSafeZoneIndicatorTransform(root.transform);
            if (marker == null)
                return;

            var indicator = root.GetComponent<WormSafeZoneIndicator>();
            if (indicator == null)
                indicator = root.AddComponent<WormSafeZoneIndicator>();

            indicator.Configure(marker.gameObject);
        }

        static Transform FindSafeZoneIndicatorTransform(Transform root)
        {
            if (root == null)
                return null;

            var direct = root.Find("SafeZoneIndicator");
            if (direct != null)
                return direct;

            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].name == "SafeZoneIndicator")
                    return children[i];
            }

            return null;
        }
    }
}
