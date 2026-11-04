using System.Collections.Generic;
using UnityEngine;

namespace FairyGUI.Editor.Workflow
{
    [CreateAssetMenu(menuName = "FGUI for Unity/Workflow Settings", fileName = "FguiWorkflowSettings")]
    public sealed class FguiWorkflowSettings : ScriptableObject
    {
        [Header("FairyGUI Editor")]
        public string fairyGuiEditorExePath = string.Empty;
        public string fairyGuiProjectPath = string.Empty;

        [Header("发布目录（支持三目录）")]
        public string publishMainFolder = string.Empty;
        public string publishSrcFolder = string.Empty;
        public string publishSubpkgFolder = string.Empty;

        [Header("Unity导入目标（建议放在本插件 Resources 下便于版本管理）")]
        public string unityTargetRootFolder = "Assets/Game/UI/FGUI/Resources/FGUIPublish";
        public bool clearTargetBeforeCopy = false;

        [Header("FGUI package descriptors (path without _fui.bytes suffix)")]
        public List<string> packageAssetPaths = new List<string>();

        [Header("快速面板默认项（仅下拉选择，用于记忆上次选项）")]
        public string defaultPackageAssetPath = string.Empty;
        public string defaultComponentName = string.Empty;
    }
}
