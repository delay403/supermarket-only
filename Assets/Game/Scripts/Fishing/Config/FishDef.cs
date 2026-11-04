using System;
using Game.Collectibles;
using UnityEngine;

namespace Game.Fishing.Config
{
    [CreateAssetMenu(menuName = "Game/Fishing/FishDef", fileName = "FishDef")]
    public sealed class FishDef : ScriptableObject
    {
        public string fishId = "fish_1";
        public string displayName = "Fish";

        [Tooltip("图鉴/商店展示用品质档位（与翅膀等共用六级）。")]
        public CosmeticQuality quality = CosmeticQuality.Common;

        [Tooltip("钓获后奖励金币（再乘养成 fishingCoinMul）；亦为图鉴展示售价。")]
        public long rewardCoins = 100;

        [Tooltip("UI 展示用图标（可选）。")]
        public Sprite icon;

        [Header("钓获表现")]
        [Tooltip("进度满后在钓鱼落点生成的世界模型预制体（可选）。")]
        public GameObject worldCatchPrefab;
    }
}
