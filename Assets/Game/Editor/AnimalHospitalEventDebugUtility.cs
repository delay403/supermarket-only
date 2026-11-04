#if UNITY_EDITOR
using Game.AnimalHospital;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class AnimalHospitalEventDebugUtility
    {
        [MenuItem("Game/Animal Hospital/Debug/Trigger Ward Fire")]
        public static void TriggerWardFire() => ResolveEvents()?.DebugTriggerWardFire();

        [MenuItem("Game/Animal Hospital/Debug/Trigger Ceiling Hazard")]
        public static void TriggerCeiling() => ResolveEvents()?.DebugTriggerCeilingEvent();

        [MenuItem("Game/Animal Hospital/Debug/Trigger Ward Shadow")]
        public static void TriggerWardShadow() => ResolveEvents()?.DebugTriggerWardShadow();

        [MenuItem("Game/Animal Hospital/Debug/Trigger Corridor Slime")]
        public static void TriggerCorridorSlime() => ResolveEvents()?.DebugTriggerCorridorSlime();

        [MenuItem("Game/Animal Hospital/Debug/Trigger Corridor Shadow")]
        public static void TriggerCorridorShadow() => ResolveEvents()?.DebugTriggerCorridorShadow();

        static AnimalHospitalEventManager ResolveEvents()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AnimalHospital] 请在运行模式下使用 Debug 事件菜单。");
                return null;
            }

            var director = Object.FindObjectOfType<AnimalHospitalDirector>();
            var events = director?.Service?.Events;
            if (events == null)
                Debug.LogWarning("[AnimalHospital] 未找到运行中的 AnimalHospitalService。");
            return events;
        }
    }
}
#endif
