using UnityEngine;

namespace Game.DestroyCity.Config
{
    public enum DestroyCityBuildingCategory
    {
        Residential = 0,
        Office = 1,
        Other = 2,
    }

    [CreateAssetMenu(menuName = "Game/DestroyCity/BuildingDef", fileName = "BuildingDef")]
    public sealed class DestroyCityBuildingDef : ScriptableObject
    {
        public DestroyCityBuildingCategory category = DestroyCityBuildingCategory.Other;
        public string displayName = "Building";
        public string tagHint = "";

        [Tooltip("摧毁该类型建筑获得的金币。")]
        [Min(0)]
        public long coinValue = 1000;
    }
}
