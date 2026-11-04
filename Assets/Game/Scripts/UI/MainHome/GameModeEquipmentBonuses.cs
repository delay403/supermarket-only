using System;
using UnityEngine;

namespace Game.UI.MainHome
{
    /// <summary>单模式装备加成开关与倍率（在 GameProductSettings 各模式项中配置）。</summary>
    [Serializable]
    public struct GameModeEquipmentBonuses
    {
        public bool pet;
        public bool wing;
        public bool trail;
        public bool character;
        public bool rod;

        [Tooltip("对该模式所有装备加成数值的全局倍率。")]
        public float statMultiplier;

        public static GameModeEquipmentBonuses AllEnabled => new GameModeEquipmentBonuses
        {
            pet = true,
            wing = true,
            trail = true,
            character = true,
            rod = true,
            statMultiplier = 1f,
        };

        public static GameModeEquipmentBonuses SharedExceptRod => new GameModeEquipmentBonuses
        {
            pet = true,
            wing = true,
            trail = true,
            character = true,
            rod = false,
            statMultiplier = 1f,
        };

        public static GameModeEquipmentBonuses ForGameplayKind(MainHomeGameplayKind kind) =>
            kind == MainHomeGameplayKind.Fishing ? AllEnabled : SharedExceptRod;
    }
}
