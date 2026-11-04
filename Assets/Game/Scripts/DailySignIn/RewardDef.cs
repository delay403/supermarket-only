using System;

namespace Game.DailySignIn
{
    [Serializable]
    public struct RewardDef
    {
        public RewardType type;
        public long amount;
        public string itemId;
    }
}

