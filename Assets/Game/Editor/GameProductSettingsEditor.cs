#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Core.Content;
using Game.UI.MainHome;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(GameProductSettings))]
    public sealed class GameProductSettingsEditor : UnityEditor.Editor
    {
        const string LegacyCatalogPath = GameContentPaths.LegacyResourcesRoot + "/MainHomeGameModeCatalog.asset";
        const float ModeRowPadding = 4f;

        SerializedProperty _modes;
        SerializedProperty _autoSyncBuildScenes;
        SerializedProperty _productMode;
        SerializedProperty _useGameplayModuleFlags;
        SerializedProperty _enabledModules;

        ReorderableList _modesList;
        readonly List<bool> _modeFoldouts = new List<bool>();
        bool _syncScheduled;

        void OnEnable()
        {
            _modes = serializedObject.FindProperty("modes");
            _autoSyncBuildScenes = serializedObject.FindProperty("autoSyncBuildScenes");
            _productMode = serializedObject.FindProperty("productMode");
            _useGameplayModuleFlags = serializedObject.FindProperty("useGameplayModuleFlags");
            _enabledModules = serializedObject.FindProperty("enabledModules");
            EnsureFoldoutSize();
            BuildModesList();
        }

        void BuildModesList()
        {
            _modesList = new ReorderableList(
                serializedObject,
                _modes,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: false)
            {
                drawHeaderCallback = DrawModesListHeader,
                elementHeightCallback = GetModeElementHeight,
                drawElementCallback = DrawModeElement,
                onAddCallback = OnAddMode,
                onReorderCallbackWithDetails = OnReorderMode,
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "统一产品配置：列表顺序 = 主页 UI 顺序（可拖拽左侧手柄调整）；勾选 enabled 的模式会显示并决定运行时模块。\n" +
                "启用玩法场景会自动写入 Build Settings，运行时本地加载。打包前会按当前配置再同步一次。",
                MessageType.Info);

            if (GUILayout.Button("打开打包统一面板（游戏模式勾选 / Build Settings 同步）", GUILayout.Height(26f)))
                GameProductPackagingWindow.Open();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_autoSyncBuildScenes, new GUIContent("Auto Sync Build Scenes"));
            if (EditorGUI.EndChangeCheck())
                ScheduleAutoSync();

            EditorGUILayout.Space(4f);
            DrawModesList();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_modes.arraySize > 0))
            {
                EditorGUILayout.LabelField("Legacy Fallback（modes 为空时生效）", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_productMode);
                EditorGUILayout.PropertyField(_useGameplayModuleFlags);
                EditorGUILayout.PropertyField(_enabledModules);
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ensure Mode Folder Layout"))
                    GameModeFolderLayoutUtility.EnsureStandardModeFolderLayoutMenu();

                if (GUILayout.Button("Normalize Configs → Content/Configs"))
                {
                    var report = GameModeContentSyncUtility.NormalizeModeConfigsToContentConfigs(log: false);
                    EditorUtility.DisplayDialog("模式配置归位", report, "确定");
                }

                if (GUILayout.Button("Auto-Wire Config References"))
                    GameModeBootstrapWiringUtility.AutoWireMenu();
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync Core Layout"))
                {
                    var report = GameModeContentSyncUtility.SyncCoreContentLayout(log: false);
                    EditorUtility.DisplayDialog("Core 内容布局", report, "确定");
                }

                if (GUILayout.Button("Preview Build Audit"))
                    GameProductBuildAuditUtility.PreviewBuildAuditMenu();
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync Build Settings Now"))
                    GameProductFusionBuildTools.SyncBuildScenesFromProductSettings((GameProductSettings)target, silent: false);

                if (GUILayout.Button("Migrate Legacy Catalog"))
                    MigrateLegacyCatalog((GameProductSettings)target);
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawModesList()
        {
            EnsureFoldoutSize();

            if (_modes.arraySize == 0)
                EditorGUILayout.HelpBox("请添加至少一个游戏模式。", MessageType.Warning);

            _modesList.DoLayoutList();
        }

        static void DrawModesListHeader(Rect rect) =>
            EditorGUI.LabelField(rect, "Game Modes（拖拽排序）");

        void OnAddMode(ReorderableList list)
        {
            _modes.InsertArrayElementAtIndex(_modes.arraySize);
            _modes.GetArrayElementAtIndex(_modes.arraySize - 1).FindPropertyRelative("enabled").boolValue = true;
            _modeFoldouts.Add(true);
            ScheduleAutoSync();
        }

        void OnReorderMode(ReorderableList list, int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= _modeFoldouts.Count || newIndex < 0 || newIndex >= _modeFoldouts.Count)
                return;

            var foldout = _modeFoldouts[oldIndex];
            _modeFoldouts.RemoveAt(oldIndex);
            _modeFoldouts.Insert(newIndex, foldout);
        }

        float GetModeElementHeight(int index)
        {
            if (index < 0 || index >= _modes.arraySize)
                return EditorGUIUtility.singleLineHeight + ModeRowPadding;

            var height = EditorGUIUtility.singleLineHeight + ModeRowPadding;
            if (!_modeFoldouts[index])
                return height;

            var element = _modes.GetArrayElementAtIndex(index);
            height += EditorGUI.GetPropertyHeight(element.FindPropertyRelative("modeId"));
            height += EditorGUI.GetPropertyHeight(element.FindPropertyRelative("displayName"));
            height += EditorGUI.GetPropertyHeight(element.FindPropertyRelative("gameplayKind"));
            height += EditorGUI.GetPropertyHeight(element.FindPropertyRelative("customSceneName"));
            height += EditorGUI.GetPropertyHeight(element.FindPropertyRelative("previewSprite"));
            height += EditorGUI.GetPropertyHeight(element.FindPropertyRelative("previewPrefab"));
            height += EditorGUI.GetPropertyHeight(element.FindPropertyRelative("mainModeSprite"));
            height += EditorGUI.GetPropertyHeight(element.FindPropertyRelative("transitionLoadingSprite"));
            height += EditorGUIUtility.singleLineHeight + 4f;
            height += GetHudVisibilityFieldsHeight(element.FindPropertyRelative("hudVisibility"));
            height += EditorGUIUtility.singleLineHeight + 4f;
            height += EditorGUIUtility.singleLineHeight + 4f;
            height += GetEquipmentBonusesFieldsHeight(element.FindPropertyRelative("equipmentBonuses"));
            height += EditorGUIUtility.singleLineHeight + 4f;
            return height + ModeRowPadding;
        }

        static float GetHudVisibilityFieldsHeight(SerializedProperty prop)
        {
            var height = 0f;
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("petHatch"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("petBackpack"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("signIn"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("character"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("luckyWheel"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("onlineReward"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("trail"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("wing"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("rodShop"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("fishCodex"));
            return height;
        }

        static float GetEquipmentBonusesFieldsHeight(SerializedProperty prop)
        {
            var height = 0f;
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("pet"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("wing"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("trail"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("character"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("rod"));
            height += EditorGUI.GetPropertyHeight(prop.FindPropertyRelative("statMultiplier"));
            return height;
        }

        void DrawModeElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= _modes.arraySize)
                return;

            var element = _modes.GetArrayElementAtIndex(index);
            var enabledProp = element.FindPropertyRelative("enabled");
            var displayNameProp = element.FindPropertyRelative("displayName");
            var title = string.IsNullOrWhiteSpace(displayNameProp.stringValue)
                ? $"Mode {index}"
                : displayNameProp.stringValue;

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var y = rect.y + 2f;
            var headerRect = new Rect(rect.x, y, rect.width - 96f, lineHeight);
            _modeFoldouts[index] = EditorGUI.Foldout(headerRect, _modeFoldouts[index], title, true);

            var toggleRect = new Rect(rect.xMax - 88f, y, 18f, lineHeight);
            EditorGUI.BeginChangeCheck();
            enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);
            if (EditorGUI.EndChangeCheck())
                ScheduleAutoSync();

            var removeRect = new Rect(rect.xMax - 64f, y, 64f, lineHeight);
            if (GUI.Button(removeRect, "Remove"))
            {
                _modes.DeleteArrayElementAtIndex(index);
                if (index >= 0 && index < _modeFoldouts.Count)
                    _modeFoldouts.RemoveAt(index);
                // Must apply before ExitGUI — otherwise Update() on next draw reverts the delete.
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                ScheduleAutoSync();
                GUIUtility.ExitGUI();
                return;
            }

            if (!_modeFoldouts[index])
                return;

            y += lineHeight + 2f;
            y = DrawPropertyField(ref y, rect, element.FindPropertyRelative("modeId"));
            y = DrawPropertyField(ref y, rect, displayNameProp);
            var kindProp = element.FindPropertyRelative("gameplayKind");
            y = DrawPropertyField(ref y, rect, kindProp);
            y = DrawPropertyField(ref y, rect, element.FindPropertyRelative("customSceneName"), "Scene Name");
            y = DrawPropertyField(ref y, rect, element.FindPropertyRelative("previewSprite"));
            y = DrawPropertyField(ref y, rect, element.FindPropertyRelative("previewPrefab"));
            y = DrawPropertyField(ref y, rect, element.FindPropertyRelative("mainModeSprite"));
            y = DrawPropertyField(ref y, rect, element.FindPropertyRelative("transitionLoadingSprite"));

            y += 4f;
            EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "HUD 按钮显隐", EditorStyles.boldLabel);
            y += lineHeight + 2f;
            y = DrawHudVisibilityRect(ref y, rect, element.FindPropertyRelative("hudVisibility"));

            var hudPresetRect = new Rect(rect.x, y, rect.width, lineHeight);
            if (GUI.Button(hudPresetRect, "按玩法类型应用 HUD 预设"))
                ApplyHudPreset(element, kindProp);
            y += lineHeight + 4f;

            EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "装备属性加成", EditorStyles.boldLabel);
            y += lineHeight + 2f;
            y = DrawEquipmentBonusesRect(ref y, rect, element.FindPropertyRelative("equipmentBonuses"));

            var equipPresetRect = new Rect(rect.x, y, rect.width, lineHeight);
            if (GUI.Button(equipPresetRect, "按玩法类型应用装备预设"))
                ApplyEquipmentPreset(element, kindProp);
        }

        static float DrawPropertyField(ref float y, Rect rect, SerializedProperty prop)
        {
            var height = EditorGUI.GetPropertyHeight(prop);
            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, height), prop, true);
            y += height + 2f;
            return y;
        }

        void ScheduleAutoSync()
        {
            var settings = (GameProductSettings)target;
            if (!settings.autoSyncBuildScenes)
                return;

            if (_syncScheduled)
                return;

            _syncScheduled = true;
            EditorApplication.delayCall += RunScheduledAutoSync;
        }

        static float DrawHudVisibilityRect(ref float y, Rect rect, SerializedProperty prop)
        {
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("petHatch"), "宠物孵化");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("petBackpack"), "宠物背包");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("signIn"), "签到");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("character"), "角色");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("luckyWheel"), "轮盘");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("onlineReward"), "在线奖励");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("trail"), "拖尾");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("wing"), "翅膀");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("rodShop"), "钓竿");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("fishCodex"), "图鉴");
            return y;
        }

        static float DrawEquipmentBonusesRect(ref float y, Rect rect, SerializedProperty prop)
        {
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("pet"), "宠物加成");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("wing"), "翅膀加成");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("trail"), "拖尾加成");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("character"), "角色加成");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("rod"), "钓竿加成");
            y = DrawPropertyField(ref y, rect, prop.FindPropertyRelative("statMultiplier"), "全局倍率");
            return y;
        }

        static float DrawPropertyField(ref float y, Rect rect, SerializedProperty prop, string label)
        {
            var height = EditorGUI.GetPropertyHeight(prop);
            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, height), prop, new GUIContent(label), true);
            y += height + 2f;
            return y;
        }

        void EnsureFoldoutSize()
        {
            while (_modeFoldouts.Count < _modes.arraySize)
                _modeFoldouts.Add(false);
            while (_modeFoldouts.Count > _modes.arraySize)
                _modeFoldouts.RemoveAt(_modeFoldouts.Count - 1);
        }

        void RunScheduledAutoSync()
        {
            _syncScheduled = false;
            if (target == null)
                return;

            var settings = (GameProductSettings)target;
            if (!settings.autoSyncBuildScenes)
                return;

            GameProductFusionBuildTools.SyncBuildScenesFromProductSettings(settings, silent: true);
        }

        static void MigrateLegacyCatalog(GameProductSettings settings)
        {
            var legacy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(LegacyCatalogPath);
            if (legacy == null)
            {
                EditorUtility.DisplayDialog("迁移", "未找到旧的 MainHomeGameModeCatalog.asset。", "确定");
                return;
            }

            var legacySo = new SerializedObject(legacy);
            var legacyModes = legacySo.FindProperty("modes");
            if (legacyModes == null || legacyModes.arraySize == 0)
            {
                EditorUtility.DisplayDialog("迁移", "旧 Catalog 中没有模式数据。", "确定");
                return;
            }

            var targetSo = new SerializedObject(settings);
            var targetModes = targetSo.FindProperty("modes");
            targetModes.arraySize = legacyModes.arraySize;

            for (var i = 0; i < legacyModes.arraySize; i++)
            {
                var source = legacyModes.GetArrayElementAtIndex(i);
                var targetEntry = targetModes.GetArrayElementAtIndex(i);
                CopyModeEntry(source, targetEntry);

                var kind = (MainHomeGameplayKind)targetEntry.FindPropertyRelative("gameplayKind").enumValueIndex;
                var module = kind switch
                {
                    MainHomeGameplayKind.WormDodge => GameplayModule.WormDodge,
                    MainHomeGameplayKind.DestroyCity => GameplayModule.DestroyCity,
                    MainHomeGameplayKind.Fishing => GameplayModule.Fishing,
                    MainHomeGameplayKind.Tower => GameplayModule.Tower,
                    _ => GameplayModule.None,
                };

                var enabled = module == GameplayModule.None
                              || !settings.useGameplayModuleFlags
                              || (settings.enabledModules & module) == module;
                targetEntry.FindPropertyRelative("enabled").boolValue = enabled;
            }

            targetSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            GameProductFusionBuildTools.SyncBuildScenesFromProductSettings(settings, silent: false);

            EditorUtility.DisplayDialog(
                "迁移完成",
                $"已将 {legacyModes.arraySize} 个模式写入 GameProductSettings。",
                "确定");
        }

        static void ApplyHudPreset(SerializedProperty element, SerializedProperty kindProp)
        {
            var kind = (MainHomeGameplayKind)kindProp.enumValueIndex;
            var preset = GameModeHudVisibility.ForGameplayKind(kind);
            var hud = element.FindPropertyRelative("hudVisibility");
            hud.FindPropertyRelative("petHatch").boolValue = preset.petHatch;
            hud.FindPropertyRelative("petBackpack").boolValue = preset.petBackpack;
            hud.FindPropertyRelative("signIn").boolValue = preset.signIn;
            hud.FindPropertyRelative("character").boolValue = preset.character;
            hud.FindPropertyRelative("luckyWheel").boolValue = preset.luckyWheel;
            hud.FindPropertyRelative("onlineReward").boolValue = preset.onlineReward;
            hud.FindPropertyRelative("trail").boolValue = preset.trail;
            hud.FindPropertyRelative("wing").boolValue = preset.wing;
            hud.FindPropertyRelative("rodShop").boolValue = preset.rodShop;
            hud.FindPropertyRelative("fishCodex").boolValue = preset.fishCodex;
        }

        static void ApplyEquipmentPreset(SerializedProperty element, SerializedProperty kindProp)
        {
            var kind = (MainHomeGameplayKind)kindProp.enumValueIndex;
            var preset = GameModeEquipmentBonuses.ForGameplayKind(kind);
            var bonuses = element.FindPropertyRelative("equipmentBonuses");
            bonuses.FindPropertyRelative("pet").boolValue = preset.pet;
            bonuses.FindPropertyRelative("wing").boolValue = preset.wing;
            bonuses.FindPropertyRelative("trail").boolValue = preset.trail;
            bonuses.FindPropertyRelative("character").boolValue = preset.character;
            bonuses.FindPropertyRelative("rod").boolValue = preset.rod;
            bonuses.FindPropertyRelative("statMultiplier").floatValue = preset.statMultiplier;
        }

        static void CopyModeEntry(SerializedProperty source, SerializedProperty target)
        {
            CopyField(source, target, "modeId");
            CopyField(source, target, "displayName");
            CopyField(source, target, "previewSprite");
            CopyField(source, target, "previewPrefab");
            CopyField(source, target, "mainModeSprite");
            CopyField(source, target, "transitionLoadingSprite");
            CopyField(source, target, "gameplayKind");
            CopyField(source, target, "customSceneName");
        }

        static void CopyField(SerializedProperty source, SerializedProperty target, string name)
        {
            var sourceField = source.FindPropertyRelative(name);
            var targetField = target.FindPropertyRelative(name);
            if (sourceField == null || targetField == null)
                return;

            switch (sourceField.propertyType)
            {
                case SerializedPropertyType.String:
                    targetField.stringValue = sourceField.stringValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    targetField.objectReferenceValue = sourceField.objectReferenceValue;
                    break;
                case SerializedPropertyType.Enum:
                    targetField.enumValueIndex = sourceField.enumValueIndex;
                    break;
                case SerializedPropertyType.Boolean:
                    targetField.boolValue = sourceField.boolValue;
                    break;
            }
        }
    }
}
#endif
