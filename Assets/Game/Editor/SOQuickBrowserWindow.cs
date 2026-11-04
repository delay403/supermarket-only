using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Collectibles.Pets;
using Game.Collectibles.Wings;
using Game.Collectibles.Trails;

namespace Game.EditorTools
{
    public sealed class SOQuickBrowserWindow : EditorWindow
    {
        private string _filter = "";
        private Vector2 _scroll;
        private int _tab; // 0 pet, 1 wing, 2 trail

        [MenuItem("Game/Tools/SO Browser")]
        public static void Open()
        {
            GetWindow<SOQuickBrowserWindow>("SO Browser");
        }

        private void OnGUI()
        {
            _tab = GUILayout.Toolbar(_tab, new[] { "Pets", "Wings", "Trails" });

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Filter", GUILayout.Width(40));
                _filter = EditorGUILayout.TextField(_filter);
                if (GUILayout.Button("Clear", GUILayout.Width(60))) _filter = "";
            }

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawList<PetDef>("t:PetDef", d => $"{d.petId} {d.displayName} [{d.quality}] Lv{d.level}"); break;
                case 1: DrawList<WingDef>("t:WingDef", d => $"{d.wingId} {d.displayName} Lv{d.level}"); break;
                case 2: DrawList<TrailDef>("t:TrailDef", d => $"{d.trailId} {d.displayName} Lv{d.level}"); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawList<T>(string searchFilter, System.Func<T, string> label) where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets(searchFilter);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null) continue;
                var text = label(asset);
                if (!string.IsNullOrWhiteSpace(_filter) && !text.ToLowerInvariant().Contains(_filter.ToLowerInvariant()))
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                        Selection.activeObject = asset;
                    GUILayout.Label(text);
                }
            }
        }
    }
}

