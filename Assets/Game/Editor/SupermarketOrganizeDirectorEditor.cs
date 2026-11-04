#if UNITY_EDITOR
using Game.EditorTools;
using Game.SupermarketOrganize;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(SupermarketOrganizeDirector))]
    public sealed class SupermarketOrganizeDirectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("自动获取 Container Zones", GUILayout.Height(28f)))
                {
                    var director = (SupermarketOrganizeDirector)target;
                    var count = SupermarketOrganizeSceneSetupUtility.AssignContainerZones(director);
                    Debug.Log($"[SupermarketOrganizeDirector] 已分配 {count} 个 Container Zone 到 {director.name}。");
                }
            }

            EditorGUILayout.HelpBox(
                "点击按钮会扫描当前场景中所有 SupermarketContainerZone（含未激活对象），按名称排序后写入 Container Zones 列表。",
                MessageType.Info);
        }
    }
}
#endif
