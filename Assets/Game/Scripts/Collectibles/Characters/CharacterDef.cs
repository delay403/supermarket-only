using UnityEngine;
using Game.Collectibles;
using Game.Collectibles.Stats;

namespace Game.Collectibles.Characters
{
    [CreateAssetMenu(menuName = "Game/Collectibles/CharacterDef", fileName = "CharacterDef")]
    public sealed class CharacterDef : ScriptableObject
    {
        public string characterId = "char_1";
        public string displayName = "Character";

        /// <summary>列表排序与解锁链（与翅膀 level 含义一致）。</summary>
        public int level = 1;

        [Tooltip("稀有度档位（共用六级品质枚举，用于格子底图等）。")]
        public CosmeticQuality quality = CosmeticQuality.Common;

        /// <summary>未拥有时在卡片上展示的星级预览（1～3）。</summary>
        [Range(1, 5)]
        public int catalogStars = 1;

        [Tooltip("存档中养成的星级上限（1～3）；后续可扩展升星花费。")]
        [Range(1, 5)]
        public int growthStarsCap = 5;

        [Tooltip("需已拥有任一角色且其 SO.level ≥ 此值才可购买当前角色（0 表示无门槛）。")]
        public int requiredUnlockedLevel;

        public CharacterCurrencyType priceCurrency = CharacterCurrencyType.Coins;

        /// <summary>对应 priceCurrency；购买时校验并扣除该项。</summary>
        public int priceAmount = 1000;

        public GameObject prefab;

        [Tooltip("背包/格子展示用肖像（可与外观预制体拆分）。")]
        public Sprite portraitIcon;

        [Tooltip("攀爬塔进度 HUD 用头像图（与 portraitIcon 列表小图区分开）。")]
        public Sprite climbTowerProgressAvatar;

        public StatModifiers modifiers;
    }
}
