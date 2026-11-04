using UnityEngine;

namespace Game.DestroyCity.Config
{
    [CreateAssetMenu(menuName = "Game/DestroyCity/FuelDef", fileName = "FuelDef")]
    public sealed class FuelDef : ScriptableObject
    {
        public string fuelId = "fuel_1";
        public string displayName = "Fuel Lv1";
        public int level = 1;

        [Tooltip("从本级升到下一级所需燃料经验。")]
        [Min(0f)]
        public float xpToNextLevel = 100f;

        [Tooltip("该等级燃料的世界模型（可选）。")]
        public GameObject worldPrefab;

        [Tooltip("HUD/图鉴展示图标（可选）。")]
        public Sprite icon;
    }
}
