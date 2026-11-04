using System.Text;
using Game.Core;
using UnityEngine;

namespace Game.Collectibles.Stats
{
    /// <summary>SO <see cref="StatModifiers"/> 在背包/商店格子与详情中的展示文案。</summary>
    public static class StatModifiersUiFormat
    {
        const float Eps = 1e-5f;

        public const string NoBonusText = "无加成";

        static readonly float[] FastTrainBreakpoints = { 0.05f, 0.1f, 0.15f, 0.2f };

        public static string FormatItemLine(StatModifiers m)
        {
            m = StatModifiersModeResolver.ResolveForActiveModule(m);
            return ResolveActiveFormatter()(m);
        }

        static System.Func<StatModifiers, string> ResolveActiveFormatter()
        {
            if (GameplayModuleContext.IsFusionProduct())
            {
                return GameplayModuleContext.ActiveModule switch
                {
                    GameplayModule.DestroyCity => FormatDestroyCityItemLine,
                    GameplayModule.Fishing => FormatFishingItemLine,
                    GameplayModule.WormDodge => FormatWormDodgeItemLine,
                    _ => FormatTowerItemLine,
                };
            }

            if (GameProductSettings.IsDestroyCityContentEnabled)
                return FormatDestroyCityItemLine;
            if (GameProductSettings.IsFishingContentEnabled)
                return FormatFishingItemLine;
            if (GameProductSettings.IsWormDodgeContentEnabled)
                return FormatWormDodgeItemLine;
            return FormatTowerItemLine;
        }

        static string FormatWormDodgeItemLine(StatModifiers m)
        {
            var sb = new StringBuilder();
            AppendWormDodgeParts(sb, m);
            return sb.Length > 0 ? sb.ToString() : NoBonusText;
        }

        static void AppendWormDodgeParts(StringBuilder sb, StatModifiers m)
        {
            if (Mathf.Abs(m.wormDodgeCoinMul) > Eps)
                Append(sb, $"金币{LeadingPlus(m.wormDodgeCoinMul * 100f)}%");

            if (Mathf.Abs(m.wormDodgeMoveSpeedMul) > Eps)
                Append(sb, $"移速{LeadingPlus(m.wormDodgeMoveSpeedMul * 100f)}%");

            if (Mathf.Abs(m.wormDodgeMoveSpeedAdd) > Eps)
                Append(sb, $"移速{LeadingPlus(m.wormDodgeMoveSpeedAdd)}");
        }

        static string FormatDestroyCityItemLine(StatModifiers m)
        {
            var sb = new StringBuilder();
            AppendDestroyCityParts(sb, m);
            return sb.Length > 0 ? sb.ToString() : NoBonusText;
        }

        static void AppendDestroyCityParts(StringBuilder sb, StatModifiers m)
        {
            if (Mathf.Abs(m.fishingPowerAdd) > Eps)
                Append(sb, $"燃料{LeadingPlus(m.fishingPowerAdd)}");

            if (Mathf.Abs(m.fishingPowerMul) > Eps)
                Append(sb, $"燃料{LeadingPlus(m.fishingPowerMul * 100f)}%");

            if (Mathf.Abs(m.fishingTrainSpeedMul) > Eps)
            {
                var index = NearestBreakpointIndex(m.fishingTrainSpeedMul, FastTrainBreakpoints);
                var pluses = Mathf.Min(index, 2);
                Append(sb, "快速生产" + new string('+', pluses));
            }

            if (Mathf.Abs(m.fishingCoinMul) > Eps)
                Append(sb, $"摧毁金币{LeadingPlus(m.fishingCoinMul * 100f)}%");
        }

        static string FormatTowerItemLine(StatModifiers m)
        {
            var sb = new StringBuilder();
            AppendTowerParts(sb, m);
            return sb.Length > 0 ? sb.ToString() : NoBonusText;
        }

        static string FormatFishingItemLine(StatModifiers m)
        {
            var sb = new StringBuilder();
            AppendFishingParts(sb, m);
            AppendTowerParts(sb, m);
            return sb.Length > 0 ? sb.ToString() : NoBonusText;
        }

        static void AppendTowerParts(StringBuilder sb, StatModifiers m)
        {
            if (Mathf.Abs(m.climbSpeedAdd) > Eps)
                Append(sb, $"速度{LeadingPlus(m.climbSpeedAdd)}");
            if (Mathf.Abs(m.climbSpeedMul) > Eps)
                Append(sb, $"速度{LeadingPlus(m.climbSpeedMul * 100f)}%");
            if (Mathf.Abs(m.coinsMul) > Eps)
                Append(sb, $"金币{LeadingPlus(m.coinsMul * 100f)}%");
            if (Mathf.Abs(m.coinsAdd) > Eps)
                Append(sb, $"金币{LeadingPlus(m.coinsAdd)}");
        }

        static void AppendFishingParts(StringBuilder sb, StatModifiers m)
        {
            if (Mathf.Abs(m.fishingPowerAdd) > Eps)
                Append(sb, $"钓鱼加成{LeadingPlus(m.fishingPowerAdd)}");

            if (Mathf.Abs(m.fishingPowerMul) > Eps)
                Append(sb, $"快速上鱼{LeadingPlus(m.fishingPowerMul * 100f)}%");

            if (Mathf.Abs(m.fishingTrainSpeedMul) > Eps)
            {
                var index = NearestBreakpointIndex(m.fishingTrainSpeedMul, FastTrainBreakpoints);
                var pluses = Mathf.Min(index, 2);
                Append(sb, "快速训练" + new string('+', pluses));
            }

            if (HasSpecialCatchBonus(m.fishingSpecialCatchBonus))
                Append(sb, "海洋眷属");
        }

        public static bool HasSpecialCatchBonus(float fishingSpecialCatchBonus) =>
            fishingSpecialCatchBonus >= 1f - Eps;

        static string FormatNearestTierLabel(string baseLabel, float value, float[] breakpoints, int maxPluses)
        {
            var index = NearestBreakpointIndex(value, breakpoints);
            var pluses = Mathf.Clamp(index, 0, maxPluses);
            return baseLabel + new string('+', pluses);
        }

        static int NearestBreakpointIndex(float value, float[] breakpoints)
        {
            var best = 0;
            var bestDist = Mathf.Abs(value - breakpoints[0]);
            for (var i = 1; i < breakpoints.Length; i++)
            {
                var dist = Mathf.Abs(value - breakpoints[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            return best;
        }

        static void Append(StringBuilder sb, string part)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(part);
        }

        static string LeadingPlus(float v)
        {
            if (Mathf.Abs(v) < Eps) return "+0";
            var abs = FormatFloat(Mathf.Abs(v));
            return v >= 0f ? "+" + abs : "-" + abs;
        }

        static string FormatFloat(float v)
        {
            if (Mathf.Abs(v - Mathf.Round(v)) < Eps)
                return Mathf.RoundToInt(v).ToString();
            return v.ToString("0.##");
        }
    }
}
