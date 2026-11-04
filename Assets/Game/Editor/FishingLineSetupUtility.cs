using Game.Fishing;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public static class FishingLineSetupUtility
    {
        [MenuItem("Game/Fishing/Setup Line On Selected Rod")]
        public static void SetupLineOnSelectedRod()
        {
            var rod = Selection.activeTransform;
            if (rod == null)
            {
                EditorUtility.DisplayDialog("Setup Fishing Line", "请先选中鱼竿根节点。", "OK");
                return;
            }

            var lineGo = rod.Find("Line");
            if (lineGo == null)
            {
                lineGo = new GameObject("Line").transform;
                lineGo.SetParent(rod, false);
            }

            lineGo.localPosition = Vector3.zero;
            lineGo.localRotation = Quaternion.identity;
            lineGo.localScale = Vector3.one;

            var lr = lineGo.GetComponent<LineRenderer>();
            if (lr == null)
                lr = lineGo.gameObject.AddComponent<LineRenderer>();

            var visual = lineGo.GetComponent<FishingLineVisual>();
            if (visual == null)
                visual = lineGo.gameObject.AddComponent<FishingLineVisual>();

            EnsureLineNodes(rod);

            var nodes = new Transform[4];
            for (var i = 0; i < 4; i++)
            {
                var n = rod.Find($"LineNode{i}");
                if (n != null)
                    nodes[i] = n;
            }

            var so = new SerializedObject(visual);
            so.FindProperty("rodLineNodes").arraySize = 4;
            for (var i = 0; i < 4; i++)
            {
                if (nodes[i] != null)
                    so.FindProperty("rodLineNodes").GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
            }

            var origin = rod.Find("LineOrigin");
            if (origin != null)
                so.FindProperty("lineOrigin").objectReferenceValue = origin;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visual);
            Debug.Log($"[FishingLineSetup] 已在 '{rod.name}' 上创建/更新 Line + FishingLineVisual。", rod);
        }

        static void EnsureLineNodes(Transform rod)
        {
            if (rod.Find("LineOrigin") == null)
            {
                var origin = new GameObject("LineOrigin").transform;
                origin.SetParent(rod, false);
                origin.localPosition = new Vector3(0f, 0.05f, 0f);
            }

            for (var i = 0; i < 4; i++)
            {
                var name = $"LineNode{i}";
                if (rod.Find(name) != null)
                    continue;

                var node = new GameObject(name).transform;
                node.SetParent(rod, false);
                node.localPosition = new Vector3(0f, 0.15f + i * 0.35f, 0f);
            }
        }
    }
}
