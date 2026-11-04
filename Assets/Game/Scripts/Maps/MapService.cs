using Game.Save;
using Game.Core.Config;
using Game.Core;
using UnityEngine;

namespace Game.Maps
{
    public static class MapService
    {
        public static bool IsUnlocked(string mapId)
        {
            if (SaveService.Data == null) return false;
            return SaveService.Data.HasUnlockedMap(mapId ?? string.Empty);
        }

        /// <summary>
        /// <see cref="ConfigRegistry.Maps"/> 列表中玩家已解锁的地图数量（用于免费金币分档等）。
        /// </summary>
        public static int GetUnlockedMapCountInCatalog()
        {
            var mapDb = ConfigRegistry.Maps;
            var data = SaveService.Data;
            if (mapDb?.maps == null || data == null)
                return 1;

            var count = 0;
            for (var i = 0; i < mapDb.maps.Count; i++)
            {
                var m = mapDb.maps[i];
                if (m == null || string.IsNullOrWhiteSpace(m.mapId))
                    continue;
                if (data.HasUnlockedMap(m.mapId))
                    count++;
            }

            return count > 0 ? count : 1;
        }

        /// <summary>全局奖牌余额（滑翔等到顶等玩法发放）。</summary>
        public static long GetGlobalMedals()
        {
            return SaveService.Data?.medals ?? 0;
        }

        public static bool TrySelectMap(string mapId)
        {
            if (!IsUnlocked(mapId)) return false;
            SaveService.Data.currentMapId = mapId;
            UnityObjectFinder.FindFirst<Game.Tower.TowerManager>()?.RefreshFromCurrentMapConfig();
            MapRuntime.RaiseChanged();
            return true;
        }

        /// <summary>登顶：按当前 <see cref="MapDef.medalsAwardOnTowerClear"/> 增加全局奖牌（≤0 则跳过）。持久化由调用方统一 <see cref="SaveService.Save"/>。</summary>
        public static int AwardTowerClearMedalsForCurrentMap()
        {
            if (SaveService.Data == null) return 0;
            var mapDb = ConfigRegistry.Maps;
            if (mapDb == null || !mapDb.TryGet(SaveService.Data.currentMapId, out var map) || map == null)
                return 0;

            var n = Mathf.Max(0, map.medalsAwardOnTowerClear);
            if (n <= 0) return 0;

            SaveService.Data.AddMedals(n);
            MapRuntime.RaiseChanged();
            return n;
        }

        public static bool CanAffordUnlock(MapDef def)
        {
            if (def == null || SaveService.Data == null) return false;
            if (IsUnlocked(def.mapId)) return true;
            return SaveService.Data.medals >= def.medalsRequiredToUnlock;
        }

        /// <summary>消耗全局奖牌解锁地图（若已解锁则返回 true 且不扣费）。</summary>
        public static bool TryUnlockWithMedals(MapDef def)
        {
            if (def == null || SaveService.Data == null) return false;
            if (IsUnlocked(def.mapId)) return true;

            var cost = def.medalsRequiredToUnlock;
            if (cost < 0) cost = 0;
            if (SaveService.Data.medals < cost) return false;

            SaveService.Data.medals -= cost;
            SaveService.Data.UnlockMap(def.mapId);
            SaveService.Save();

            UnityObjectFinder.FindFirst<Game.Tower.TowerManager>()?.RefreshFromCurrentMapConfig();
            MapRuntime.RaiseChanged();
            return true;
        }

        /// <summary>写入当前地图并跳转 gameplay 场景（场景名为空则仅改存档与塔配置）。</summary>
        public static bool TeleportToMap(MapDef def)
        {
            if (def == null || SaveService.Data == null) return false;
            if (!IsUnlocked(def.mapId)) return false;

            SaveService.Data.currentMapId = def.mapId;
            SaveService.Save();

            UnityObjectFinder.FindFirst<Game.Tower.TowerManager>()?.RefreshFromCurrentMapConfig();
            MapRuntime.RaiseChanged();

            GameSceneLoader.LoadMap(def);
            return true;
        }
    }
}
