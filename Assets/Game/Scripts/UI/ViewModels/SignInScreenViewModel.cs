using System.Collections.Generic;
using System.Text;
using Game.Core.Config;
using Game.DailySignIn;
using Game.Save;
using Game.UI;
using UnityEngine;

namespace Game.UI.ViewModels
{
    public enum SignInSlotState
    {
        LockedFuture = 0,
        AvailableToday = 1,
        Claimed = 2,
        Tomorrow = 3,
    }

    public sealed class SignInSlotViewModel
    {
        public int DayNumber;
        public RewardDef Reward;
        public SignInSlotState State;
        public bool IsGrandPrize;

        /// <summary>来自 <see cref="SignInDayDef.rewardIcon"/>；非空则覆盖 <see cref="RewardPresentation.TryResolveIcon"/>。</summary>
        public Sprite RewardIconOverride;
    }

    public sealed class SignInScreenViewModel
    {
        public string GameDayLocalId;
        public bool SignedInToday;
        public bool CanClaimToday;
        public long Coins;
        public long Gems;
        public List<SignInSlotViewModel> Slots = new List<SignInSlotViewModel>();
        public bool ConfigMissing;

        /// <summary>
        /// signInDayIndex 表示「下一次要领的是第几天」（0→Day1 …）；领完后递增，满 7 归零。
        /// signedInToday && signInDayIndex==0 表示本游戏日内刚领完一轮 Day7（全部显示已领）。
        /// </summary>
        public static SignInScreenViewModel Build()
        {
            SignInService.RefreshDailyFlags();

            var vm = new SignInScreenViewModel
            {
                GameDayLocalId = SignInService.GameDayIdLocal,
                SignedInToday = SaveService.Data != null && SaveService.Data.signedInToday,
                CanClaimToday = SignInService.CanClaimToday(),
                Coins = SaveService.Data?.coins ?? 0,
                Gems = SaveService.Data?.gems ?? 0,
            };

            var db = ConfigRegistry.SignIn;
            if (db == null || db.days == null || db.days.Count == 0)
            {
                vm.ConfigMissing = true;
                return vm;
            }

            var data = SaveService.Data;
            var nextIdx = data != null ? data.signInDayIndex : 0;
            var signed = data != null && data.signedInToday;
            var cycleCompleteToday = signed && nextIdx == 0;

            foreach (var day in db.days)
            {
                if (day == null) continue;
                var dn = day.dayNumber;
                var state = ResolveSlotState(dn, nextIdx, signed, cycleCompleteToday);
                vm.Slots.Add(new SignInSlotViewModel
                {
                    DayNumber = dn,
                    Reward = day.reward,
                    State = state,
                    IsGrandPrize = day.isGrandPrize,
                    RewardIconOverride = day.rewardIcon,
                });
            }

            return vm;
        }

        static SignInSlotState ResolveSlotState(int dayNumber, int nextIdx, bool signedInToday, bool cycleCompleteToday)
        {
            if (cycleCompleteToday)
                return SignInSlotState.Claimed;

            var claimed = nextIdx > 0 && dayNumber <= nextIdx;
            if (claimed)
                return SignInSlotState.Claimed;

            var nextRewardDay = nextIdx + 1;

            if (!signedInToday && dayNumber == nextRewardDay)
                return SignInSlotState.AvailableToday;

            if (signedInToday && dayNumber == nextRewardDay)
                return SignInSlotState.Tomorrow;

            return SignInSlotState.LockedFuture;
        }

        public string ToInfoText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Daily Sign-In");
            sb.AppendLine($"Game day (local): {GameDayLocalId}");
            sb.AppendLine($"Signed today: {SignedInToday}");
            sb.AppendLine();

            if (ConfigMissing)
            {
                sb.AppendLine("SignInDatabase not found. Put one under Resources/SignInDatabase.asset.");
            }
            else
            {
                foreach (var s in Slots)
                {
                    sb.AppendLine($"Day {s.DayNumber}: {RewardPresentation.FormatRewardLine(s.Reward)} [{s.State}]");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"Coins: {Coins}  Gems: {Gems}");
            return sb.ToString();
        }
    }
}
