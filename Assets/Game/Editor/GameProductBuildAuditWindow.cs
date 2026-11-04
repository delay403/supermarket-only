#if UNITY_EDITOR
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public sealed class GameProductBuildAuditWindow : EditorWindow
    {
        const float MinWidth = 720f;
        const float MinHeight = 420f;

        string _report = string.Empty;
        int _issueCount;
        Vector2 _scroll;

        public static void Show(GameProductSettings settings)
        {
            if (settings == null)
            {
                EditorUtility.DisplayDialog("打包审计", "未找到 GameProductSettings.asset。", "确定");
                return;
            }

            var window = GetWindow<GameProductBuildAuditWindow>(utility: true, title: "Build Content Audit", focus: true);
            window._issueCount = GameProductBuildAuditUtility.CollectIssues(settings).Count;
            window._report = GameProductBuildAuditUtility.BuildAuditReport(settings);
            window.minSize = new Vector2(MinWidth, MinHeight);
            window.Show();
            window.Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                var summary = _issueCount == 0
                    ? "未发现跨模式引用链泄漏。"
                    : $"发现 {_issueCount} 项问题";
                EditorGUILayout.LabelField(summary, EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("复制报告", GUILayout.Width(88f)))
                    EditorGUIUtility.systemCopyBuffer = _report;

                if (GUILayout.Button("刷新", GUILayout.Width(64f)))
                {
                    var settings = GameProductFusionBuildTools.LoadProductSettingsAsset();
                    if (settings != null)
                    {
                        _issueCount = GameProductBuildAuditUtility.CollectIssues(settings).Count;
                        _report = GameProductBuildAuditUtility.BuildAuditReport(settings);
                    }
                }
            }

            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
            };
            EditorGUILayout.TextArea(_report, style, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("关闭", GUILayout.Height(24f)))
                Close();
        }
    }
}
#endif
