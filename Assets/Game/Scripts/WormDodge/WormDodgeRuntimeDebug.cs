namespace Game.WormDodge
{
    /// <summary>
    /// 躲避虫子模式运行时调试总开关（由 <see cref="WormDodgeDirector"/> 配置）。
    /// 关闭后可避免 Inspector 字段刷新、Scene Gizmo 与安全区几何扫描等调试开销。
    /// </summary>
    public static class WormDodgeRuntimeDebug
    {
        public static bool InspectorFieldsEnabled { get; private set; }
        public static bool SceneGizmosEnabled { get; private set; }

        public static bool AnyEnabled => InspectorFieldsEnabled || SceneGizmosEnabled;

        public static void Configure(bool inspectorFields, bool sceneGizmos)
        {
            InspectorFieldsEnabled = inspectorFields;
            SceneGizmosEnabled = sceneGizmos;
        }

        public static void SetAllEnabled(bool enabled)
        {
            InspectorFieldsEnabled = enabled;
            SceneGizmosEnabled = enabled;
        }
    }
}
