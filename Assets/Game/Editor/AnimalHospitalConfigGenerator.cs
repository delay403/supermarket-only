#if UNITY_EDITOR
using System.IO;
using Game.AnimalHospital.Config;
using Game.Core.Content;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class AnimalHospitalConfigGenerator
    {
        [MenuItem("Game/Animal Hospital/Generate Config Assets")]
        public static void Generate()
        {
            var root = GameContentPaths.AnimalHospitalConfigRoot.Replace('\\', '/');
            Directory.CreateDirectory(root);

            var medicine = CreateOrLoad<AnimalHospitalMedicineCatalog>(
                Path.Combine(root, "AnimalHospitalMedicineCatalog.asset"));
            var illness = CreateOrLoad<AnimalHospitalIllnessCatalog>(
                Path.Combine(root, "AnimalHospitalIllnessCatalog.asset"));
            var patient = CreateOrLoad<AnimalHospitalPatientCatalog>(
                Path.Combine(root, "AnimalHospitalPatientCatalog.asset"));
            var config = CreateOrLoad<AnimalHospitalConfig>(
                Path.Combine(root, "AnimalHospitalConfig.asset"));

            config.medicineCatalog = medicine;
            config.illnessCatalog = illness;
            config.patientCatalog = patient;

            EditorUtility.SetDirty(medicine);
            EditorUtility.SetDirty(illness);
            EditorUtility.SetDirty(patient);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AnimalHospital] Configs generated at {root}");
            Selection.activeObject = config;
        }

        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            path = path.Replace('\\', '/');
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
#endif
