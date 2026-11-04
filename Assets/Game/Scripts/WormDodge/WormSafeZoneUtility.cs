using Game.EscapeSnake;
using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>安全区占用查询（以 Trigger 注册为准）。</summary>
    public static class WormSafeZoneUtility
    {
        static WormSafeZone[] _cachedZones = System.Array.Empty<WormSafeZone>();
        static float _cacheAt;
        static bool _directorZonesActive;

        public static void SetDirectorZones(WormSafeZone[] zones)
        {
            _cachedZones = zones ?? System.Array.Empty<WormSafeZone>();
            _cacheAt = Time.time;
            _directorZonesActive = _cachedZones.Length > 0;
        }

        public static void InvalidateCache()
        {
            _directorZonesActive = false;
            _cachedZones = System.Array.Empty<WormSafeZone>();
            _cacheAt = 0f;
        }

        public static WormSafeZone[] GetZones()
        {
            if (_directorZonesActive)
                return _cachedZones;

            if (_cachedZones.Length > 0 && Time.time - _cacheAt < 5f)
                return _cachedZones;

#if UNITY_2023_1_OR_NEWER
            _cachedZones = Object.FindObjectsByType<WormSafeZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            _cachedZones = Object.FindObjectsOfType<WormSafeZone>();
#endif
            _cacheAt = Time.time;
            return _cachedZones;
        }

        public static bool ShouldShowSafeZoneIndicator(WormDodgeParticipant participant) =>
            participant != null && participant.IsInSafeZone;

        public static bool IsRegisteredInAnyZone(WormDodgeParticipant participant) =>
            FindRegisteredZone(participant) != null;

        public static WormSafeZone FindRegisteredZone(WormDodgeParticipant participant)
        {
            if (participant == null)
                return null;

            var zones = GetZones();
            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone != null && zone.ContainsParticipant(participant))
                    return zone;
            }

            return null;
        }

        public static void RefreshParticipantDebugFields(WormDodgeParticipant participant,
            out bool registeredInZone,
            out bool insideZoneGeometry,
            out bool shouldShowIndicator,
            out WormSafeZone registeredZone)
        {
            registeredInZone = false;
            insideZoneGeometry = false;
            shouldShowIndicator = false;
            registeredZone = null;

            if (participant == null)
                return;

            registeredZone = FindRegisteredZone(participant);
            registeredInZone = registeredZone != null || participant.IsInSafeZone;
            insideZoneGeometry = registeredInZone;
            shouldShowIndicator = participant.IsInSafeZone;
        }
    }
}
