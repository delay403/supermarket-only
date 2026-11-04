#if UNITY_EDITOR
using Game.Audio;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class SupermarketOrganizeBootstrapUtility
    {
        const string AudioFolder = "Assets/Audio/SuperMarket";

        [MenuItem("Game/Supermarket Organize/Wire SFX To Game Bootstrapper")]
        public static void WireSfxToGameBootstrapper()
        {
            var bootstrap = Object.FindObjectOfType<GameBootstrapper>();
            if (bootstrap == null)
            {
                EditorUtility.DisplayDialog("超市收纳", "当前场景未找到 GameBootstrapper。", "确定");
                return;
            }

            var so = new SerializedObject(bootstrap);
            var library = so.FindProperty("bootstrapSfxLibrary");
            if (library == null || !library.isArray)
            {
                EditorUtility.DisplayDialog("超市收纳", "GameBootstrapper 缺少 bootstrapSfxLibrary。", "确定");
                return;
            }

            Upsert(library, SfxId.SupermarketOrganizePickUp, LoadClip("拾取.mp3"));
            Upsert(library, SfxId.SupermarketOrganizePlace, LoadClip("放置正确.mp3"));
            Upsert(library, SfxId.SupermarketOrganizeSelect, LoadClip("选中交互.mp3"));
            Upsert(library, SfxId.SupermarketOrganizePlaceWrong, LoadClip("放置错误.mp3"));
            Upsert(library, SfxId.SupermarketOrganizeShelfComplete, LoadClip("全部摆放完毕.mp3"));

            var victory = LoadClip("胜利.mp3");
            if (victory != null)
                Upsert(library, SfxId.SettlementWin, victory);

            var bgm = LoadClip("背景音乐.mp3");
            if (bgm != null)
            {
                var bgmProp = so.FindProperty("bootstrapBgmClip");
                if (bgmProp != null)
                    bgmProp.objectReferenceValue = bgm;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            EditorUtility.DisplayDialog("超市收纳", "已将超市收纳音效写入 GameBootstrapper。", "确定");
        }

        public static void EnsureBootstrapSupermarketSfx(SerializedObject bootstrapSo)
        {
            if (bootstrapSo == null)
                return;

            var library = bootstrapSo.FindProperty("bootstrapSfxLibrary");
            if (library == null || !library.isArray)
                return;

            Upsert(library, SfxId.SupermarketOrganizePickUp, LoadClip("拾取.mp3"));
            Upsert(library, SfxId.SupermarketOrganizePlace, LoadClip("放置正确.mp3"));
            Upsert(library, SfxId.SupermarketOrganizeSelect, LoadClip("选中交互.mp3"));
            Upsert(library, SfxId.SupermarketOrganizePlaceWrong, LoadClip("放置错误.mp3"));
            Upsert(library, SfxId.SupermarketOrganizeShelfComplete, LoadClip("全部摆放完毕.mp3"));

            var victory = LoadClip("胜利.mp3");
            if (victory != null)
                Upsert(library, SfxId.SettlementWin, victory);

            var bgm = LoadClip("背景音乐.mp3");
            if (bgm != null)
            {
                var bgmProp = bootstrapSo.FindProperty("bootstrapBgmClip");
                if (bgmProp != null)
                    bgmProp.objectReferenceValue = bgm;
            }
        }

        static AudioClip LoadClip(string fileName) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{fileName}");

        static void Upsert(SerializedProperty library, SfxId id, AudioClip clip)
        {
            if (clip == null)
                return;

            for (var i = 0; i < library.arraySize; i++)
            {
                var element = library.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("id").intValue != (int)id)
                    continue;

                element.FindPropertyRelative("clip").objectReferenceValue = clip;
                element.FindPropertyRelative("volumeScale").floatValue = 1f;
                return;
            }

            var idx = library.arraySize;
            library.InsertArrayElementAtIndex(idx);
            var added = library.GetArrayElementAtIndex(idx);
            added.FindPropertyRelative("id").intValue = (int)id;
            added.FindPropertyRelative("clip").objectReferenceValue = clip;
            added.FindPropertyRelative("volumeScale").floatValue = 1f;
        }
    }
}
#endif
