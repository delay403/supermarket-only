#if UNITY_EDITOR
using System.IO;
using Game.AnimalHospital;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class AnimalHospitalAudioSetupUtility
    {
        const string AudioFolder = "Assets/Game/Modes/AnimalHospital/Audio";

        [MenuItem("Game/Animal Hospital/Assign Audio Clips On Directors")]
        public static void AssignAudioClips()
        {
            var directors = Object.FindObjectsOfType<AnimalHospitalDirector>(true);
            var assigned = 0;
            for (var i = 0; i < directors.Length; i++)
            {
                var dir = directors[i];
                var audio = dir.GetComponent<AnimalHospitalAudio>();
                if (audio == null)
                    audio = dir.gameObject.AddComponent<AnimalHospitalAudio>();

                if (AssignClips(audio))
                {
                    EditorUtility.SetDirty(audio);
                    EditorUtility.SetDirty(dir.gameObject);
                    assigned++;
                }
            }

            // Also assign any standalone AnimalHospitalAudio in open scenes / prefabs selection
            var all = Object.FindObjectsOfType<AnimalHospitalAudio>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (AssignClips(all[i]))
                {
                    EditorUtility.SetDirty(all[i]);
                    assigned++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimalHospital] Audio clips assigned on {assigned} component(s). Folder={AudioFolder}");
        }

        public static bool AssignClips(AnimalHospitalAudio audio)
        {
            if (audio == null)
                return false;

            var so = new SerializedObject(audio);
            var changed = false;
            changed |= SetClip(so, "bgm", "bgm");
            changed |= SetClip(so, "interactComplete", "通用交互");
            changed |= SetClip(so, "freezeGunLoop", "冷冻枪");
            changed |= SetClip(so, "pistolShot", "开枪");
            changed |= SetClip(so, "cameraShutter", "拍照");
            changed |= SetClip(so, "lowSanityHeartbeatLoop", "残血心跳");
            changed |= SetClip(so, "extinguisherLoop", "灭火器");
            changed |= SetClip(so, "batonSwing", "球棍攻击");
            changed |= SetClip(so, "taserLoop", "电击器攻击");
            changed |= SetClip(so, "computerAnalysisLoop", "电脑仪器分析中");
            changed |= SetClip(so, "wardFireLoop", "着火");
            changed |= SetClip(so, "receptionWaitLoop", "顾客等待服务");

            var hurt = so.FindProperty("patientHurtClips");
            if (hurt != null)
            {
                var c1 = FindClip("受伤1");
                var c2 = FindClip("受伤2");
                var c3 = FindClip("受伤3");
                if (c1 != null || c2 != null || c3 != null)
                {
                    hurt.arraySize = 3;
                    if (c1 != null) hurt.GetArrayElementAtIndex(0).objectReferenceValue = c1;
                    if (c2 != null) hurt.GetArrayElementAtIndex(1).objectReferenceValue = c2;
                    if (c3 != null) hurt.GetArrayElementAtIndex(2).objectReferenceValue = c3;
                    changed = true;
                }
            }

            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        static bool SetClip(SerializedObject so, string property, string fileNameWithoutExt)
        {
            var prop = so.FindProperty(property);
            if (prop == null)
                return false;
            if (prop.objectReferenceValue != null)
                return false;

            var clip = FindClip(fileNameWithoutExt);
            if (clip == null)
                return false;

            prop.objectReferenceValue = clip;
            return true;
        }

        static AudioClip FindClip(string fileNameWithoutExt)
        {
            if (!Directory.Exists(AudioFolder))
                return null;

            var guids = AssetDatabase.FindAssets($"{fileNameWithoutExt} t:AudioClip", new[] { AudioFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var name = Path.GetFileNameWithoutExtension(path);
                if (name == fileNameWithoutExt)
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            // Fallback: contains match
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            return null;
        }
    }
}
#endif
