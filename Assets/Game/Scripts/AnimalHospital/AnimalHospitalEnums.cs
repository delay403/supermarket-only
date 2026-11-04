namespace Game.AnimalHospital
{
    public enum AnimalHospitalInteractableKind
    {
        Report = 0,
        Camera = 1,
        Computer = 2,
        Printer = 3,
        AdmissionForm = 4,
        Patient = 5,
        Analyzer = 6,
        Medicine = 7,
        CoffeeMachine = 8,
        Prop = 9,
        WardComputer = 10,
        WardDisplay = 11,
        Curtain = 12,
        FireTarget = 13,
        Slime = 14,
        Shadow = 15,
        /// <summary>换班结算台：交互后打开日结算面板。</summary>
        DaySettlement = 16,
    }

    public enum AnimalHospitalPropId
    {
        None = 0,
        StunBaton = 1,
        Taser = 2,
        Pistol = 3,
        FireExtinguisher = 4,
        FreezeGun = 5,
    }

    public enum AnimalHospitalPatientPhase
    {
        Spawning = 0,
        Waiting = 1,
        MovingToReception = 2,
        AtReception = 3,
        MovingToWard = 4,
        InWard = 5,
        BeingTreated = 6,
        Leaving = 7,
        Gone = 8,
    }

    public enum AnimalHospitalReceptionStep
    {
        None = 0,
        Report = 1,
        Camera = 2,
        Computer = 3,
        Printer = 4,
        AdmissionForm = 5,
        Patient = 6,
        Completed = 7,
    }

    public enum AnimalHospitalWardStep
    {
        None = 0,
        /// <summary>病人躺床后先进行手术，再进分析机流程。</summary>
        Surgery = 1,
        Analyzer = 2,
        WardComputer = 3,
        WardDisplay = 4,
        TreatPatient = 5,
        Completed = 6,
    }
}
