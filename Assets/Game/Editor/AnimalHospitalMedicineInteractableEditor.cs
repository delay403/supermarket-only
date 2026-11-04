#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.AnimalHospital.Editor
{
    [CustomEditor(typeof(AnimalHospitalMedicineInteractable))]
    public sealed class AnimalHospitalMedicineInteractableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("medicineId"),
                new GUIContent("Medicine Id", "对应 Medicine Catalog 中的 medicineId"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("holdSecondsOverride"),
                new GUIContent("Hold Seconds Override", "≤0 使用 Config 默认药物交互时间"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("interactable"),
                new GUIContent("Interactable"));

            var med = (AnimalHospitalMedicineInteractable)target;
            EditorGUILayout.Space(6f);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Display Name (from Catalog)", med.DisplayName);
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
