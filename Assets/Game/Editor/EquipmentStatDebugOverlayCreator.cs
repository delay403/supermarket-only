using Game.Debugging;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public static class EquipmentStatDebugOverlayCreator
    {
        const string MenuPath = "Game/Debug/Create Equipment Stat Debug Overlay";

        [MenuItem(MenuPath)]
        static void CreateOverlayInScene()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = Object.FindFirstObjectByType<EquipmentStatDebugOverlay>();
#else
            var existing = Object.FindObjectOfType<EquipmentStatDebugOverlay>();
#endif
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                Debug.LogWarning("场景中已存在 EquipmentStatDebugOverlay，已选中。");
                return;
            }

            var go = new GameObject("EquipmentStatDebugOverlay");
            go.AddComponent<EquipmentStatDebugOverlay>();
            Undo.RegisterCreatedObjectUndo(go, "Create Equipment Stat Debug Overlay");
            Selection.activeGameObject = go;
            Debug.Log(
                "已创建带 EquipmentStatDebugOverlay 的空物体。请将其换为你场景中的调试 Canvas，或把组件挂到 DebugCanvas 上：关闭 rebuildUiFromCode，在 Inspector 绑定主 Panel、打开/关闭按钮与各 InputField。");
        }
    }
}
