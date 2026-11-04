namespace Game.AnimalHospital
{
    public sealed class AnimalHospitalCoffeeMachine : AnimalHospitalInteractable
    {
        void Awake() => Configure(AnimalHospitalInteractableKind.CoffeeMachine, "coffee", "咖啡机");
    }
}
