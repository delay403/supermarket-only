namespace Game.AnimalHospital
{
    public sealed class AnimalHospitalAdmissionForm : AnimalHospitalInteractable
    {
        public AnimalHospitalPatient LinkedPatient { get; private set; }

        public void BindPatient(AnimalHospitalPatient patient)
        {
            LinkedPatient = patient;
            Configure(AnimalHospitalInteractableKind.AdmissionForm, "admission", "入院单");
        }
    }
}
