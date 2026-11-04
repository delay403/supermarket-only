using System.Collections.Generic;
using System.Text;
using Game.Core.Config;
using Game.Maps;
using Game.Save;
using UnityEngine;

namespace Game.UI.ViewModels
{
    public sealed class MapRowViewModel
    {
        public string MapId;
        public string DisplayName;
        public Sprite Icon;
        public bool Unlocked;
        public int MedalsRequiredToUnlock;
        public float MaxTowerHeight;
    }

    public sealed class MapSelectViewModel
    {
        public List<MapRowViewModel> Rows = new List<MapRowViewModel>();
        public string CurrentMapId;

        /// <summary>全局奖牌余额（解锁地图时消耗）。</summary>
        public long PlayerMedals;

        public static MapSelectViewModel Build()
        {
            var vm = new MapSelectViewModel();
            if (SaveService.Data != null)
            {
                vm.CurrentMapId = SaveService.Data.currentMapId;
                vm.PlayerMedals = SaveService.Data.medals;
            }

            var db = ConfigRegistry.Maps;
            if (db == null) return vm;

            foreach (var m in db.maps)
            {
                if (m == null) continue;
                vm.Rows.Add(new MapRowViewModel
                {
                    MapId = m.mapId,
                    DisplayName = m.displayName,
                    Icon = m.mapIcon,
                    Unlocked = MapService.IsUnlocked(m.mapId),
                    MedalsRequiredToUnlock = m.medalsRequiredToUnlock,
                    MaxTowerHeight = m.maxTowerHeight,
                });
            }

            return vm;
        }

        public string ToListText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Medals (global): {PlayerMedals}");
            sb.AppendLine("Maps:");
            foreach (var r in Rows)
            {
                sb.AppendLine($"- {r.DisplayName} ({r.MapId}) unlocked:{r.Unlocked} cost:{r.MedalsRequiredToUnlock} maxHeight:{r.MaxTowerHeight}");
            }

            sb.AppendLine();
            sb.AppendLine($"Current: {CurrentMapId ?? "(none)"}");
            return sb.ToString();
        }
    }
}
