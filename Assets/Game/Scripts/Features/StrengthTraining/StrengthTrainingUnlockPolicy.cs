namespace Game.Features.StrengthTraining
{
    public enum StrengthTrainingUnlockPolicy
    {
        /// <summary>始终可用（基础训练场）。</summary>
        AlwaysOpen = 0,

        /// <summary>每日看一次免费解锁。</summary>
        FreeOncePerDay = 1,

        /// <summary>每日消耗钻石解锁。</summary>
        GemOncePerDay = 2,

        /// <summary>每日累计 N 次免费解锁（见 <see cref="StrengthTrainingGroundDef.claimsRequired"/>）。</summary>
        ProgressPerDay = 3,
    }
}
