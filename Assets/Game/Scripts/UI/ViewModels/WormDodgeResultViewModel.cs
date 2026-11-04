namespace Game.UI.ViewModels
{
    public sealed class WormDodgeResultViewModel
    {
        public bool IsVictory;
        public Game.WormDodge.WormDodgeEndReason EndReason;
        public int FloorsReached;
        public long CoinsEarned;
        public int BestFloorsRecord;
        public float RunClearTimeSeconds;
        public float BestClearTimeSeconds;
        public bool CanRevive;
        public bool DoubleRewardClaimed;
    }

    public sealed class WormDodgeFloorRewardViewModel
    {
        public int FloorIndex;
        public long CoinReward;
    }
}
