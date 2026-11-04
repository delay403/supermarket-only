using System;
using UnityEngine;

namespace Game.Collectibles
{
    [Serializable]
    public sealed class QualityVisualEntry
    {
        public CosmeticQuality tier = CosmeticQuality.Common;

        [Tooltip("展示用中文名（如列表富文本、「品质」行）。若留空则用内置默认名。")]
        public string displayNameOverride;

        [Tooltip("富文本 <color=#...> 用；品质词着色。")]
        public Color richTextColor = Color.white;

        [Tooltip("商店格子等区域用的品质底图。")]
        public Sprite slotBackground;

        [Tooltip("详情区可选底图（空则留白底）。")]
        public Sprite detailBackground;
    }

    [CreateAssetMenu(menuName = "Game/Collectibles/Quality Visual Library", fileName = "QualityVisualLibrary")]
    public sealed class QualityVisualLibrary : ScriptableObject
    {
        public QualityVisualEntry[] entries = Array.Empty<QualityVisualEntry>();

        public bool TryGet(CosmeticQuality tier, out QualityVisualEntry entry)
        {
            if (entries != null)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    var e = entries[i];
                    if (e != null && e.tier == tier)
                    {
                        entry = e;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        /// <summary>富文本用词（内置名或 Override）。</summary>
        public static string BuiltinDisplayName(CosmeticQuality tier)
        {
            return tier switch
            {
                CosmeticQuality.Common => "普通",
                CosmeticQuality.Excellent => "优秀",
                CosmeticQuality.Rare => "稀有",
                CosmeticQuality.Epic => "史诗",
                CosmeticQuality.Legendary => "传说",
                CosmeticQuality.Mythic => "神话",
                _ => "普通",
            };
        }

        public string ResolveDisplayName(CosmeticQuality tier)
        {
            if (TryGet(tier, out var e) && !string.IsNullOrWhiteSpace(e.displayNameOverride))
                return e.displayNameOverride;
            return BuiltinDisplayName(tier);
        }

        public Color ResolveRichTextColor(CosmeticQuality tier)
        {
            if (TryGet(tier, out var e))
                return e.richTextColor;
            return Color.white;
        }

        [Header("神话品质动态色")]
        [Tooltip("品质词彩虹色相变化速度（完整色相环/秒）。0 表示不用动画，始终用条目里的固有色。")]
        [SerializeField] float mythicHueCyclesPerSecond = 0.1f;
        [Tooltip("彩虹饱和度（0~1）。")]
        [SerializeField] float mythicRainbowSaturation = 0.82f;
        [Tooltip("彩虹明度（0~1）。")]
        [SerializeField] float mythicRainbowValue = 1f;

        /// <summary>用于 UI 神话品质文字：色相随时间流动。</summary>
        public Color ResolveMythicRainbowColor(float unscaledTime)
        {
            if (mythicHueCyclesPerSecond <= 0f)
                return ResolveRichTextColor(CosmeticQuality.Mythic);
            var h = (unscaledTime * mythicHueCyclesPerSecond) % 1f;
            if (h < 0f) h += 1f;
            return Color.HSVToRGB(h, mythicRainbowSaturation, mythicRainbowValue);
        }

        /// <summary>
        /// 「品质：xxx」富文本一行。神话档位的品质词使用 <see cref="ResolveMythicRainbowColor"/>，
        /// 需在 <c>Update</c> 中每帧刷新绑定 Text 才有流动效果。
        /// </summary>
        public string FormatQualityLineRichText(CosmeticQuality tier, float mythicHueTimeUnscaled)
        {
            var nm = ResolveDisplayName(tier);
            if (tier == CosmeticQuality.Mythic)
            {
                var c = ResolveMythicRainbowColor(mythicHueTimeUnscaled);
                return $"<color=#BBBBBB>品质：</color><color=#{ColorUtility.ToHtmlStringRGB(c)}>{nm}</color>";
            }

            var hex = ColorUtility.ToHtmlStringRGB(ResolveRichTextColor(tier));
            return $"<color=#BBBBBB>品质：</color><color=#{hex}>{nm}</color>";
        }

        /// <summary>lib 为空时使用内置名与简易彩虹（神话）。</summary>
        public static string FormatQualityLineRichTextLoose(QualityVisualLibrary lib, CosmeticQuality tier,
            float mythicHueTimeUnscaled)
        {
            if (lib != null)
                return lib.FormatQualityLineRichText(tier, mythicHueTimeUnscaled);
            return FormatQualityLineRichTextBuiltin(tier, mythicHueTimeUnscaled);
        }

        static string FormatQualityLineRichTextBuiltin(CosmeticQuality tier, float mythicHueTimeUnscaled)
        {
            var nm = BuiltinDisplayName(tier);
            if (tier == CosmeticQuality.Mythic)
            {
                var c = Color.HSVToRGB((mythicHueTimeUnscaled * 0.1f) % 1f, 0.82f, 1f);
                return $"<color=#BBBBBB>品质：</color><color=#{ColorUtility.ToHtmlStringRGB(c)}>{nm}</color>";
            }

            var hex = ColorUtility.ToHtmlStringRGB(Color.white);
            return $"<color=#BBBBBB>品质：</color><color=#{hex}>{nm}</color>";
        }

        public Sprite ResolveSlotBackground(CosmeticQuality tier)
        {
            TryGet(tier, out var e);
            return e?.slotBackground;
        }

        public Sprite ResolveDetailBackground(CosmeticQuality tier)
        {
            TryGet(tier, out var e);
            return e?.detailBackground;
        }
    }
}
