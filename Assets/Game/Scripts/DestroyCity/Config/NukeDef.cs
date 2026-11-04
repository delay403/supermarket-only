using UnityEngine;

namespace Game.DestroyCity.Config
{
    [CreateAssetMenu(menuName = "Game/DestroyCity/NukeDef", fileName = "NukeDef")]
    public sealed class NukeDef : ScriptableObject
    {
        public string nukeId = "nuke_1";
        public string displayName = "Nuke Lv1";
        public int level = 1;

        [Tooltip("从上一级升到此级所需的燃料经验。")]
        [Min(0f)]
        public float upgradeXpRequired = 100f;

        [Tooltip("爆炸半径（米），影响摧毁规模与金币结算。")]
        [Min(1f)]
        public float explosionRadius = 50f;

        [Tooltip("摧毁结算金币倍率（乘城市基础奖励）。")]
        [Min(0.01f)]
        public float coinRewardMultiplier = 1f;

        [Tooltip("发射器上展示的核弹外观预制体。")]
        public GameObject launcherPrefab;

        [Tooltip("爆炸特效预制体（可选）。")]
        public GameObject explosionVfxPrefab;

        [Tooltip("商店/HUD 展示图标（可选）。")]
        public Sprite icon;
    }
}
