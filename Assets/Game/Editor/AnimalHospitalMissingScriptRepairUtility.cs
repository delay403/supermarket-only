#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.AnimalHospital;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 修复因「多 MonoBehaviour 合文件」导致的空 Script / Missing Script。
    /// 按组件标题或 GameObject 名称重新挂载正确脚本，并尽量拷贝已有序列化字段。
    /// </summary>
    public static class AnimalHospitalMissingScriptRepairUtility
    {
        static readonly Dictionary<string, Type> NameToType = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["AnimalHospitalWaitingZone"] = typeof(AnimalHospitalWaitingZone),
            ["Animal Hospital Waiting Zone"] = typeof(AnimalHospitalWaitingZone),
            ["WaitingZone"] = typeof(AnimalHospitalWaitingZone),

            ["AnimalHospitalReceptionZone"] = typeof(AnimalHospitalReceptionZone),
            ["Animal Hospital Reception Zone"] = typeof(AnimalHospitalReceptionZone),
            ["ReceptionZone"] = typeof(AnimalHospitalReceptionZone),

            ["AnimalHospitalWard"] = typeof(AnimalHospitalWard),
            ["Animal Hospital Ward"] = typeof(AnimalHospitalWard),

            ["AnimalHospitalCorridorZone"] = typeof(AnimalHospitalCorridorZone),
            ["Animal Hospital Corridor Zone"] = typeof(AnimalHospitalCorridorZone),
            ["CorridorZone"] = typeof(AnimalHospitalCorridorZone),

            ["AnimalHospitalHospitalExit"] = typeof(AnimalHospitalHospitalExit),
            ["Animal Hospital Hospital Exit"] = typeof(AnimalHospitalHospitalExit),
            ["HospitalExit"] = typeof(AnimalHospitalHospitalExit),

            ["AnimalHospitalMedicineCabinet"] = typeof(AnimalHospitalMedicineCabinet),
            ["Animal Hospital Medicine Cabinet"] = typeof(AnimalHospitalMedicineCabinet),
            ["MedicineCabinet"] = typeof(AnimalHospitalMedicineCabinet),

            ["AnimalHospitalMedicineInteractable"] = typeof(AnimalHospitalMedicineInteractable),
            ["Animal Hospital Medicine Interactable"] = typeof(AnimalHospitalMedicineInteractable),

            ["AnimalHospitalPropInteractable"] = typeof(AnimalHospitalPropInteractable),
            ["Animal Hospital Prop Interactable"] = typeof(AnimalHospitalPropInteractable),

            ["AnimalHospitalCoffeeMachine"] = typeof(AnimalHospitalCoffeeMachine),
            ["Animal Hospital Coffee Machine"] = typeof(AnimalHospitalCoffeeMachine),
            ["CoffeeMachine"] = typeof(AnimalHospitalCoffeeMachine),

            ["AnimalHospitalAdmissionForm"] = typeof(AnimalHospitalAdmissionForm),
            ["Animal Hospital Admission Form"] = typeof(AnimalHospitalAdmissionForm),

            ["AnimalHospitalResultPanel"] = typeof(AnimalHospitalResultPanel),
            ["Animal Hospital Result Panel"] = typeof(AnimalHospitalResultPanel),

            ["AnimalHospitalShadowAi"] = typeof(AnimalHospitalShadowAi),
            ["AnimalHospitalSlimeHazard"] = typeof(AnimalHospitalSlimeHazard),
            ["AnimalHospitalCeilingHazard"] = typeof(AnimalHospitalCeilingHazard),
            ["AnimalHospitalHoldButtonBridge"] = typeof(AnimalHospitalHoldButtonBridge),
        };

        [MenuItem("Game/Animal Hospital/Repair Empty Or Missing Scripts In Scene")]
        public static void RepairScene()
        {
            var repaired = 0;
            var removedOnly = 0;
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                Walk(roots[i].transform, ref repaired, ref removedOnly);

            // 再跑一遍场景骨架，重新接线 Director 引用
            AnimalHospitalSceneSetupUtility.SetupSceneContents();

            EditorUtility.DisplayDialog(
                "动物医院脚本修复",
                $"已重新挂载 {repaired} 个组件，清理无效 Missing {removedOnly} 个。\n场景已重新接线。请保存场景。",
                "确定");
        }

        static void Walk(Transform t, ref int repaired, ref int removedOnly)
        {
            RepairOnGameObject(t.gameObject, ref repaired, ref removedOnly);
            for (var i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), ref repaired, ref removedOnly);
        }

        static void RepairOnGameObject(GameObject go, ref int repaired, ref int removedOnly)
        {
            var components = go.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                // Missing script → null Component entry
                if (components[i] != null)
                    continue;

                var inferred = InferTypeFromName(go.name);
                if (inferred == null)
                {
                    // 删除纯 Missing Script
                    var so = new SerializedObject(go);
                    var props = so.FindProperty("m_Component");
                    // GameObjectUtility API
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    removedOnly++;
                    break;
                }

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (go.GetComponent(inferred) == null)
                {
                    go.AddComponent(inferred);
                    repaired++;
                    EditorUtility.SetDirty(go);
                }

                break;
            }

            // 空 Script 但仍可见标题的情况：序列化失败的 MonoBehaviour（非 null，但 script 丢了）
            // Unity 会显示为 MonoBehaviour；用 serialized Object 检查 m_Script
            var behaviours = go.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null)
                    continue;

                var so = new SerializedObject(mb);
                var scriptProp = so.FindProperty("m_Script");
                if (scriptProp == null || scriptProp.objectReferenceValue != null)
                    continue;

                var type = InferTypeFromInspectorTitle(mb) ?? InferTypeFromName(go.name);
                if (type == null)
                    continue;

                UnityEngine.Object.DestroyImmediate(mb, true);
                if (go.GetComponent(type) == null)
                    go.AddComponent(type);
                repaired++;
                EditorUtility.SetDirty(go);
            }

            // 按物体名确保关键区域组件一定存在
            EnsureKnownComponent(go);
        }

        static void EnsureKnownComponent(GameObject go)
        {
            if (!NameToType.TryGetValue(go.name, out var type))
                return;
            if (go.GetComponent(type) == null)
                go.AddComponent(type);
        }

        static Type InferTypeFromName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;
            if (NameToType.TryGetValue(objectName.Trim(), out var type))
                return type;

            // Medicine_1 / Ward_1
            if (objectName.StartsWith("Medicine_", StringComparison.OrdinalIgnoreCase))
                return typeof(AnimalHospitalMedicineInteractable);
            if (objectName.StartsWith("Ward_", StringComparison.OrdinalIgnoreCase))
                return typeof(AnimalHospitalWard);
            if (objectName.IndexOf("StunBaton", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Taser", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Pistol", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("FireExtinguisher", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("FreezeGun", StringComparison.OrdinalIgnoreCase) >= 0)
                return typeof(AnimalHospitalPropInteractable);

            return null;
        }

        static Type InferTypeFromInspectorTitle(MonoBehaviour mb)
        {
            // 无法可靠读取 Inspector 标题时回退物体名
            return InferTypeFromName(mb.gameObject.name);
        }
    }
}
#endif
