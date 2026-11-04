using Game.Save;
using Game.UI.Framework;

namespace Game.Economy
{
    /// <summary>
    /// 与 <see cref="Game.UI.Effects.UICoinFlyEffect"/> 配合：按视觉金币枚数拆分奖励，逐枚入账并刷新 HUD。
    /// </summary>
    public static class EconomyCoinFlyGrant
    {
        public static long[] SplitAmount(long total, int count)
        {
            if (count <= 0 || total <= 0)
                return System.Array.Empty<long>();

            var chunks = new long[count];
            var perCoin = total / count;
            var extra = total % count;
            for (var i = 0; i < count; i++)
                chunks[i] = perCoin + (i < extra ? 1L : 0L);
            return chunks;
        }

        public static void GrantChunk(long amount)
        {
            if (amount <= 0)
                return;

            if (UiCoordinator.Hud != null)
            {
                UiCoordinator.Hud.AddCoinsDisplay(amount);
                return;
            }

            var data = SaveService.Data;
            if (data == null)
                return;

            data.coins += amount;
            SaveService.Save();
            EconomyDisplayEvents.Raise();
        }
    }
}
