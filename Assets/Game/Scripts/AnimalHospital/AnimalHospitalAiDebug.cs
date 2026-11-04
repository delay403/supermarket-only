using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>医院 AI 病人调试日志（Console 过滤关键字：AH-AI）。</summary>
    public static class AnimalHospitalAiDebug
    {
        public static bool Enabled = true;

        public static void Log(string message)
        {
            if (!Enabled)
                return;

            Debug.Log($"[AH-AI] {message}");
        }

        public static void Log(AnimalHospitalPatient patient, string message)
        {
            if (!Enabled || patient == null)
                return;

            var pos = patient.ActorTransform != null
                ? patient.ActorTransform.position
                : patient.transform.position;
            Debug.Log(
                $"[AH-AI][{patient.PatientId}|{patient.DisplayName}] phase={patient.Phase} pos=({pos.x:F1},{pos.y:F1},{pos.z:F1}) {message}",
                patient);
        }

        public static void Warn(AnimalHospitalPatient patient, string message)
        {
            if (!Enabled || patient == null)
                return;

            Debug.LogWarning(
                $"[AH-AI][{patient.PatientId}|{patient.DisplayName}] phase={patient.Phase} {message}",
                patient);
        }
    }
}
