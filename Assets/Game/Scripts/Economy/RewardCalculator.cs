using System.Text;
using UnityEngine;
using Game.Core.Config;
using Game.Save;

namespace Game.Economy
{
    public static class RewardCalculator
    {
        /// <summary>为 false 时可关闭 <see cref="CalculateGlideSettlement"/> 内的金币推导日志。</summary>
        public static bool LogGlideCoinSettlement = true;

        /// <summary>
        /// 本地图滑翔金币 = (基础金币 + totalCoinsAdd) × max(0, 1 + totalCoinsMul + 地图金币修正) × 滑翔距离 / glideSettlementCoinDistanceDivisor。
        /// 本地图滑翔钻石 = floor(水平滑翔距离 / 1000 × gemsPer1000m)（见 <see cref="Game.Core.Config.GameBalanceConfig.gemsPer1000m"/>，默认约每 1000m 一钻）。
        /// 除数默认为 1000（见 <see cref="Game.Core.Config.GameBalanceConfig.glideSettlementCoinDistanceDivisor"/>）。
        /// 基础金币默认 500，除数 1000、无加成、地图修正 0 时 100m 水平距离约为 50 币。
        /// </summary>
        public static Rewards CalculateGlideSettlement(float towerHeightMeters, float glideDistanceXZ, float totalCoinsAdd, float totalCoinsMul)
        {
            var bal = ConfigRegistry.Balance;
            var cfgBase = bal != null ? bal.glideSettlementBaseCoins : 500f;
            // 旧 SO 未序列化该字段时可能为 0，回退到 500。
            var baseCoins = cfgBase > 0.01f ? cfgBase : 500f;
            var cfgDiv = bal != null ? bal.glideSettlementCoinDistanceDivisor : 0f;
            var coinDistDivisor = cfgDiv > 0.01f ? cfgDiv : 1000f;
            var gemsPer1000 = bal != null ? bal.gemsPer1000m : 1f;

            var mapCoinCorrection = 0f;
            string mapLabel = "(无地图)";
            var mapDb = ConfigRegistry.Maps;
            if (mapDb != null && SaveService.Data != null && mapDb.TryGet(SaveService.Data.currentMapId, out var map) && map != null)
            {
                mapCoinCorrection = map.glideCoinsMapCorrection;
                mapLabel = $"{map.displayName} [{map.mapId}]";
            }

            var rawBracket = 1f + totalCoinsMul + mapCoinCorrection;
            var bracket = Mathf.Max(0f, rawBracket);
            var dist = Mathf.Max(0f, glideDistanceXZ);
            var coinsFloat = (baseCoins + totalCoinsAdd) * bracket * dist / coinDistDivisor;

            // 钻石：按「水平滑翔距离」每 1000m × gemsPer1000m（与 SO 注释一致；不再用爬塔高度）。
            var gemsFloat = (dist / 1000f) * gemsPer1000;

            var coinsFlooredBeforeBuff = (long)Mathf.Floor(coinsFloat);
            var coinsBuffActive = Game.Reward.RewardBuffService.IsCoinsBuffActive();

            var r = new Rewards
            {
                coins = coinsFlooredBeforeBuff,
                gems = (long)Mathf.Floor(gemsFloat),
                medalCount = 0
            };

            if (coinsBuffActive)
                r.coins *= 2;

            if (LogGlideCoinSettlement)
            {
                var basePlusAdd = baseCoins + totalCoinsAdd;
                var productBeforeDiv = basePlusAdd * bracket * dist;

                var sb = new StringBuilder(1024);
                sb.AppendLine("======== [GlideSettlement] 滑翔结算 · 经济与金币推演 ========");
                sb.AppendLine($"  ── GameBalance（SO 原始 → 运行时采用） ──");
                sb.AppendLine($"  glideSettlementBaseCoins: {cfgBase:F4} → baseCoins={baseCoins:F4}");
                sb.AppendLine($"  glideSettlementCoinDistanceDivisor: {cfgDiv:F4} → coinDistDivisor={coinDistDivisor:F4}");
                sb.AppendLine($"  gemsPer1000m: {gemsPer1000:F4}");
                sb.AppendLine($"  ── 地图（当前存档 currentMapId） ──");
                sb.AppendLine($"  {mapLabel}");
                sb.AppendLine($"  glideCoinsMapCorrection: {mapCoinCorrection:F4}");
                sb.AppendLine($"  ── 存档 / 运行时上下文 ──");
                sb.AppendLine(
                    SaveService.Data != null
                        ? $"  currentMapId: {SaveService.Data.currentMapId}"
                        : "  SaveService.Data: （空）");
                sb.AppendLine($"  ── 本场结算输入（滑翔） ──");
                sb.AppendLine($"  towerHeightMeters（展示用）: {towerHeightMeters:F4} m");
                sb.AppendLine($"  glideDistanceXZ（水平距离，金币+钻石用）: {dist:F4} m");
                sb.AppendLine($"  ── 养成 Σ（由 StatsAggregator 合并后传入） ──");
                sb.AppendLine($"  totalCoinsAdd (ΣcoinsAdd): {totalCoinsAdd:F4}");
                sb.AppendLine($"  totalCoinsMul (ΣcoinsMul): {totalCoinsMul:F4}");
                sb.AppendLine($"  ── 金币括号 ──");
                sb.AppendLine($"  rawBracket = 1 + ΣcoinsMul + 地图金币修正 = 1 + {totalCoinsMul:F4} + {mapCoinCorrection:F4} = {rawBracket:F4}");
                sb.AppendLine($"  bracket = max(0, rawBracket) = {bracket:F4}");
                sb.AppendLine($"  ── 公式（与代码一致） ──");
                sb.AppendLine(
                    "  coinsFloat = (baseCoins + ΣcoinsAdd) × bracket × glideDistanceXZ ÷ coinDistDivisor");
                sb.AppendLine(
                    $"           = ({baseCoins:F4} + {totalCoinsAdd:F4}) × {bracket:F4} × {dist:F4} ÷ {coinDistDivisor:F4}");
                sb.AppendLine(
                    $"           = {basePlusAdd:F4} × {bracket:F4} × {dist:F4} ÷ {coinDistDivisor:F4}");
                sb.AppendLine($"           = {productBeforeDiv:F4} ÷ {coinDistDivisor:F4} = {coinsFloat:F6}");
                sb.AppendLine($"  coinsFlooredBeforeBuff = floor(coinsFloat) = {coinsFlooredBeforeBuff}");
                sb.AppendLine(
                    $"  激励双倍金币 Buff 激活 = {coinsBuffActive}（见 RewardBuffService.IsCoinsBuffActive / 存档 coinsBuffEndUtcTicks）");
                if (coinsBuffActive)
                    sb.AppendLine($"  coinsAfterBuff = {coinsFlooredBeforeBuff} × 2 = {r.coins}");
                else
                    sb.AppendLine($"  coinsAfterBuff（无 Buff）= {r.coins}");
                sb.AppendLine($"  >>> 本场结算下发金币总数（写入存档）: {r.coins}");
                sb.AppendLine($"  ── 钻石（水平滑翔距离） ──");
                sb.AppendLine(
                    $"  gemsFloat = (glideDistanceXZ ÷ 1000) × gemsPer1000m = ({dist:F4}/1000)×{gemsPer1000:F4} = {gemsFloat:F6}");
                sb.AppendLine($"  >>> floor → 本场钻石下发: {r.gems}");
                sb.AppendLine("======== [GlideSettlement] END ========");
                Debug.Log(sb.ToString());
            }

            return r;
        }
    }
}
