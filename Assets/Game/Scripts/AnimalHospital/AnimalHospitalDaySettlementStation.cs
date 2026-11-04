namespace Game.AnimalHospital
{
    /// <summary>换班结算交互台：长按交互打开日结算面板。</summary>
    public sealed class AnimalHospitalDaySettlementStation : AnimalHospitalInteractable
    {
        void Awake() => Configure(AnimalHospitalInteractableKind.DaySettlement, "day_settlement", "换班报告");
    }
}
