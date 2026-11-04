using UnityEditor;
using UnityEngine;
using Game.Collectibles.Pets;
using Game.Collectibles.Wings;
using Game.Collectibles.Trails;

namespace Game.EditorTools
{
    [CustomEditor(typeof(PetDef))]
    public sealed class PetDefEditor : UnityEditor.Editor
    {
        private static Texture _icon;

        public override void OnInspectorGUI()
        {
            EnsureIcon();
            DrawHeader("Pet", _icon);
            DrawDefaultInspector();
        }

        private static void EnsureIcon()
        {
            if (_icon != null) return;
            _icon = EditorGUIUtility.IconContent("d_Favorite Icon").image;
        }

        private static void DrawHeader(string title, Texture icon)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(icon, GUILayout.Width(22), GUILayout.Height(22));
                    GUILayout.Label(title, EditorStyles.boldLabel);
                }
            }
        }
    }

    [CustomEditor(typeof(WingDef))]
    public sealed class WingDefEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawHeader("Wing", EditorGUIUtility.IconContent("d_SceneViewFx").image);
            DrawDefaultInspector();
        }

        private static void DrawHeader(string title, Texture icon)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(icon, GUILayout.Width(22), GUILayout.Height(22));
                    GUILayout.Label(title, EditorStyles.boldLabel);
                }
            }
        }
    }

    [CustomEditor(typeof(TrailDef))]
    public sealed class TrailDefEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var icon = EditorGUIUtility.IconContent("ParticleSystem Icon");
            if (icon == null || icon.image == null)
                icon = EditorGUIUtility.IconContent("d_Transform Icon");
            DrawHeader("Trail", icon != null ? icon.image : null);
            DrawDefaultInspector();
        }

        private static void DrawHeader(string title, Texture icon)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(icon, GUILayout.Width(22), GUILayout.Height(22));
                    GUILayout.Label(title, EditorStyles.boldLabel);
                }
            }
        }
    }
}

