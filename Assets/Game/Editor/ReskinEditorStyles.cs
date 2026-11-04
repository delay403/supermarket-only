using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    static class ReskinEditorStyles
    {
        static bool initialized;

        public static GUIStyle WindowPadding;
        public static GUIStyle SectionHeader;
        public static GUIStyle SectionCard;
        public static GUIStyle PathLabel;
        public static GUIStyle ScopeLabel;
        public static GUIStyle PrimaryButton;
        public static GUIStyle SecondaryButton;
        public static GUIStyle DangerButton;
        public static GUIStyle HistoryCard;
        public static GUIStyle FooterNote;

        public static Color Accent => EditorGUIUtility.isProSkin
            ? new Color(0.35f, 0.62f, 0.98f)
            : new Color(0.12f, 0.42f, 0.82f);

        public static Color CardBg => EditorGUIUtility.isProSkin
            ? new Color(0.18f, 0.19f, 0.22f, 0.95f)
            : new Color(0.93f, 0.94f, 0.96f, 0.98f);

        public static Color CardBorder => EditorGUIUtility.isProSkin
            ? new Color(0.32f, 0.34f, 0.38f)
            : new Color(0.75f, 0.78f, 0.82f);

        public static void EnsureInitialized()
        {
            if (initialized)
                return;

            WindowPadding = new GUIStyle { padding = new RectOffset(12, 12, 10, 12) };

            SectionHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 4, 6)
            };

            SectionCard = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(10, 10, 8, 10),
                margin = new RectOffset(0, 0, 0, 8)
            };

            PathLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.65f, 0.7f, 0.78f) : new Color(0.35f, 0.38f, 0.42f) }
            };

            ScopeLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Italic
            };

            PrimaryButton = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                fixedHeight = 34
            };

            SecondaryButton = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 24
            };

            DangerButton = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 24
            };

            HistoryCard = new GUIStyle(SectionCard)
            {
                margin = new RectOffset(0, 0, 0, 6)
            };

            FooterNote = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                wordWrap = true,
                margin = new RectOffset(0, 0, 8, 0)
            };

            initialized = true;
        }

        public static void DrawSectionTitle(string title, string subtitle = null)
        {
            EnsureInitialized();
            EditorGUILayout.LabelField(title, SectionHeader);
            if (!string.IsNullOrEmpty(subtitle))
                EditorGUILayout.LabelField(subtitle, PathLabel);
        }

        public static void BeginCard()
        {
            EnsureInitialized();
            EditorGUILayout.BeginVertical(SectionCard);
        }

        public static void EndCard() => EditorGUILayout.EndVertical();

        public static void DrawPath(string label, string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            EditorGUILayout.LabelField(label, PathLabel);
            EditorGUILayout.SelectableLabel(path, PathLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
    }
}
