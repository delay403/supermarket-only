using System;
using Game.Collectibles.Stats;
using Game.Core.Config;
using Game.DestroyCity.Config;
using Game.Economy;
using Game.Save;
using UnityEngine;

namespace Game.DestroyCity
{
    public static class NukeLaunchService
    {
        public struct LaunchResult
        {
            public bool success;
            public long coinsAwarded;
            public float destructionRatio;
            public float explosionRadius;
            public NukeDef nuke;
            public DestroyCityMapDef map;
        }

        public static event Action<LaunchResult> LaunchCompleted;

        static DestroyCityModeSaveData Data => SaveService.Data?.destroyCity;

        public static LaunchResult LaunchAtCity(
            DestroyCityMapDef map,
            DestroyCityStatsAggregator.FinalDestroyCityStats stats,
            NukeDef nuke = null)
        {
            var fail = new LaunchResult { success = false };
            if (map == null || Data == null)
                return fail;

            nuke ??= NukeUpgradeService.GetCurrentNukeDef();
            if (nuke == null)
                return fail;

            var destructionRatio = ComputeDestructionRatio(nuke.explosionRadius, map.cityScaleMeters);

            Data.totalLaunches++;
            if (Data.totalLaunches == 1)
                DiscountOfferService.NotifyFirstNukeLaunch();

            if (destructionRatio >= 0.5f)
                Data.totalCitiesDestroyed++;

            SaveService.Save();

            var result = new LaunchResult
            {
                success = true,
                coinsAwarded = 0,
                destructionRatio = destructionRatio,
                explosionRadius = nuke.explosionRadius,
                nuke = nuke,
                map = map,
            };

            LaunchCompleted?.Invoke(result);
            return result;
        }

        public static float ComputeDestructionRatio(float explosionRadius, float cityScaleMeters)
        {
            if (cityScaleMeters <= 0.01f)
                return 0f;

            var bal = ConfigRegistry.Balance;
            var cap = bal != null && bal.destroyCityMaxDestructionRatio > 0f
                ? bal.destroyCityMaxDestructionRatio
                : 1f;

            var ratio = explosionRadius / cityScaleMeters;
            return Mathf.Clamp01(ratio * cap);
        }

        public static DestroyCityMapDef ResolveCurrentMapDef()
        {
            var db = ConfigRegistry.DestroyCityMaps;
            var id = Data?.currentDestroyCitySceneId ?? "destroy_city_map_1";
            if (db != null && db.TryGet(id, out var def))
                return def;
            return null;
        }
    }
}
