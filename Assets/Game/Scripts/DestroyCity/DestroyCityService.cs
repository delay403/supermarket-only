using Game.Core.Config;
using Game.DestroyCity.Config;
using Game.Save;

namespace Game.DestroyCity
{
    /// <summary>摧毁城市玩法门面：地图解析、存档读写。</summary>
    public static class DestroyCityService
    {
        static DestroyCityModeSaveData Data => SaveService.Data?.destroyCity;

        public static string CurrentMapId => Data?.currentDestroyCitySceneId ?? "destroy_city_map_1";

        public static bool TrySelectMap(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId) || Data == null)
                return false;

            var db = ConfigRegistry.DestroyCityMaps;
            if (db == null || !db.TryGet(mapId, out _))
                return false;

            Data.currentDestroyCitySceneId = mapId.Trim();
            SaveService.Save();
            return true;
        }

        public static DestroyCityMapDef GetCurrentMapDef() => NukeLaunchService.ResolveCurrentMapDef();

        public static string ResolveGameplaySceneName()
        {
            var def = GetCurrentMapDef();
            if (def != null && !string.IsNullOrWhiteSpace(def.gameplaySceneName))
                return def.gameplaySceneName.Trim();

            var cfg = Game.Maps.SceneNavigationSettings.ResolveActive();
            return cfg != null && !string.IsNullOrWhiteSpace(cfg.destroyCityMainSceneName)
                ? cfg.destroyCityMainSceneName.Trim()
                : "DestroyCityScene";
        }
    }
}
