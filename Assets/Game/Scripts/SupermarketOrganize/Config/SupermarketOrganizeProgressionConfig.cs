using Game.Core.Content;
using UnityEngine;

namespace Game.SupermarketOrganize.Config
{
    [CreateAssetMenu(menuName = "Game/Supermarket Organize/Progression Config", fileName = "SupermarketOrganizeProgressionConfig")]
    public sealed class SupermarketOrganizeProgressionConfig : ScriptableObject
    {
        [Header("Attributes — Handle Capacity (Lv0..6)")]
        public int[] handleCapacityByLevel = { 4, 6, 8, 10, 12, 14, 16 };
        public int[] handleCapacityUpgradeCosts = { 60, 90, 135, 203, 304, 456 };

        [Header("Attributes — Move Speed Bonus (Lv0..5)")]
        public float[] moveSpeedBonusByLevel = { 0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
        public int[] moveSpeedUpgradeCosts = { 50, 73, 152, 194, 245 };

        [Header("Attributes — Pickup Range (Lv0..4)")]
        public float[] pickupRangeByLevel = { 12f, 15f, 18f, 21f, 24f };
        public int[] pickupRangeUpgradeCosts = { 55, 80, 116, 170 };

        [Header("Skills — Scanner Cooldown (Lv0..4)")]
        public float[] scannerCooldownByLevel = { 14f, 12f, 10f, 8f, 6f };
        public int[] scannerUpgradeCosts = { 90, 135, 203, 304 };
        public float scannerHighlightDurationSeconds = 8f;

        [Header("Skills — Magnet")]
        public float[] magnetCooldownByLevel = { 17.5f, 15f, 12.5f, 10f, 7.5f, 5f };
        public int[] magnetUpgradeCosts = { 120, 180, 270, 405, 608 };
        public float magnetPullRadius = 10f;

        [Header("Skills — X-Ray")]
        public float[] xRayCooldownByLevel = { 16f, 13f, 10f, 7f };
        public int[] xRayUpgradeCosts = { 80, 116, 168 };
        public float xRayTipDurationSeconds = 6f;

        [Header("Skills — Auto Stocker")]
        public float[] autoStockerCooldownByLevel = { 60f, 55f, 50f, 45f, 40f, 35f };
        public int[] autoStockerUpgradeCosts = { 150, 225, 338, 506, 759 };

        [Header("Sweep Robot")]
        public float sweepRobotUnlockDurationSeconds = 300f;
        public float sweepRobotIntervalSeconds = 15f;

        public static SupermarketOrganizeProgressionConfig ResolveRuntime()
        {
            var loaded = GameContentService.Resolve<SupermarketOrganizeProgressionConfig>();
            return loaded != null ? loaded : CreateRuntimeFallback();
        }

        static SupermarketOrganizeProgressionConfig CreateRuntimeFallback()
        {
            var config = CreateInstance<SupermarketOrganizeProgressionConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            return config;
        }
    }
}
