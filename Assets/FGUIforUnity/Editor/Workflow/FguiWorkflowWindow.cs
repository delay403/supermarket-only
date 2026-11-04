using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UitkTextField = UnityEngine.UIElements.TextField;
using FGUIPackage = FairyGUI.UIPackage;
using FGUIPanel = FairyGUI.UIPanel;
using FGUIPackageItemType = FairyGUI.PackageItemType;

namespace FairyGUI.Editor.Workflow
{
    public sealed class FguiWorkflowWindow : EditorWindow
    {
        private const string SettingsAssetPath = "Assets/FGUIforUnity/Editor/Workflow/FguiWorkflowSettings.asset";
        private const string SettingsBackupJsonPath = "ProjectSettings/FguiWorkflowSettings.backup.json";
        private const string UssPath = "Assets/FGUIforUnity/Editor/Workflow/Fgui-workflow.uss";
        private const string DefaultUnityPublishRoot = "Assets/Game/UI/FGUI/Resources/FGUIPublish";

        private enum TabId
        {
            Packages = 0,
            Publish = 1,
            QuickPanel = 2,
            References = 3
        }

        private FguiWorkflowSettings _settings;
        private TabId _tab;

        // Root UI
        private Label _healthLabel;
        private VisualElement _healthBar;
        private VisualElement _contentRoot;
        private readonly Dictionary<TabId, Button> _tabButtons = new Dictionary<TabId, Button>();

        // Package tab
        private ListView _packageListView;
        private UitkTextField _newPackageField;
        private Label _packageOpResult;

        // Publish tab
        private UitkTextField _exeField;
        private UitkTextField _fairyProjectField;
        private UitkTextField _publishMainField;
        private UitkTextField _publishSrcField;
        private UitkTextField _publishSubpkgField;
        private UitkTextField _unityTargetField;
        private Toggle _clearBeforeCopyToggle;
        private Label _publishResultLabel;

        // Quick panel tab（仅下拉选择包与组件）
        private PopupField<string> _packagePathPopup;
        private PopupField<string> _componentPopup;
        private VisualElement _quickComponentBlock;
        private Label _quickPackageReadout;
        private Label _quickComponentReadout;
        private Label _quickSelectionInfoLabel;
        private readonly List<string> _componentNames = new List<string>();

        // References tab
        private UitkTextField _searchField;
        private VisualElement _searchResultContainer;
        private int _copyFileCount;

        private sealed class SearchHit
        {
            public string assetPath;
            public int line;
            public string preview;
        }

        [MenuItem("FGUI for Unity/工作台")]
        public static void Open()
        {
            var win = GetWindow<FguiWorkflowWindow>("FGUI工作台");
            win.minSize = new Vector2(980, 640);
        }

        [MenuItem("FGUI for Unity/创建设置资产")]
        public static void EnsureSettingsAsset()
        {
            var settings = LoadOrCreateSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private void OnEnable()
        {
            _settings = LoadOrCreateSettings();
        }

        private void OnDisable()
        {
            SaveSettings();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Clear();

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null)
                rootVisualElement.styleSheets.Add(uss);
            rootVisualElement.AddToClassList("fgui-root");
            ApplySafeEditorBuiltinFont();

            BuildLayout();
            RefreshAll();
        }

        private void BuildLayout()
        {
            _healthBar = new VisualElement();
            _healthBar.AddToClassList("health-bar");
            _healthLabel = new Label("健康检查...");
            _healthLabel.AddToClassList("health-text");
            _healthBar.Add(_healthLabel);
            rootVisualElement.Add(_healthBar);

            var top = new VisualElement();
            top.AddToClassList("top-row");
            var tabs = new VisualElement();
            tabs.AddToClassList("tabs-row");
            top.Add(tabs);

            var saveBtn = new Button(() => SaveSettings()) { text = "统一保存设置" };
            saveBtn.AddToClassList("btn");
            saveBtn.AddToClassList("btn-primary");
            top.Add(saveBtn);
            rootVisualElement.Add(top);

            CreateTabButton(tabs, TabId.Packages, "包管理");
            CreateTabButton(tabs, TabId.Publish, "发布工作流");
            CreateTabButton(tabs, TabId.QuickPanel, "快速面板");
            CreateTabButton(tabs, TabId.References, "引用查找");

            _contentRoot = new ScrollView();
            _contentRoot.AddToClassList("content-root");
            rootVisualElement.Add(_contentRoot);
        }

        private void CreateTabButton(VisualElement parent, TabId id, string text)
        {
            var btn = new Button(() => SwitchTab(id)) { text = text };
            btn.AddToClassList("tab-btn");
            parent.Add(btn);
            _tabButtons[id] = btn;
        }

        private void SwitchTab(TabId id)
        {
            _tab = id;
            ApplySafeEditorBuiltinFont();
            RebuildTabContent();
            RefreshTabButtonStyles();
        }

        private void RefreshTabButtonStyles()
        {
            foreach (var kv in _tabButtons)
            {
                kv.Value.EnableInClassList("tab-btn-active", kv.Key == _tab);
            }
        }

        private void RebuildTabContent()
        {
            _contentRoot.Clear();
            switch (_tab)
            {
                case TabId.Packages: BuildPackagesTab(); break;
                case TabId.Publish: BuildPublishTab(); break;
                case TabId.QuickPanel: BuildQuickPanelTab(); break;
                case TabId.References: BuildReferencesTab(); break;
            }
        }

        private void BuildPackagesTab()
        {
            var card = Card("FGUI包注册表");
            _contentRoot.Add(card);

            var actionRow = Row();
            actionRow.AddToClassList("row-wrap");
            actionRow.Add(MakeBtn("自动扫描 *_fui.bytes", DiscoverPackages, "btn"));
            actionRow.Add(MakeBtn("通过文件添加包...", AddPackageByBrowse, "btn"));
            actionRow.Add(MakeBtn("去重+排序", NormalizePackageList, "btn"));
            actionRow.Add(MakeBtn("加载全部", LoadAllRegistered, "btn"));
            actionRow.Add(MakeBtn("卸载全部", UnloadAllRegistered, "btn"));
            card.Add(actionRow);

            var addRow = Row();
            _newPackageField = new UitkTextField("新增包路径");
            StyleFormField(_newPackageField, true);
            addRow.Add(_newPackageField);
            addRow.Add(MakeBtn("添加", () =>
            {
                if (string.IsNullOrWhiteSpace(_newPackageField.value)) return;
                var p = NormalizePath(_newPackageField.value.Trim());
                if (!_settings.packageAssetPaths.Contains(p))
                    _settings.packageAssetPaths.Add(p);
                _newPackageField.value = "";
                SaveSettings();
                RefreshAll();
            }, "btn-primary"));
            card.Add(addRow);

            _packageListView = new ListView
            {
                itemsSource = _settings.packageAssetPaths,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.Single,
                fixedItemHeight = 26,
                makeItem = () =>
                {
                    var label = new Label();
                    label.AddToClassList("package-item-label");
                    return label;
                },
                bindItem = (e, i) => ((Label)e).text = _settings.packageAssetPaths[i]
            };
            _packageListView.AddToClassList("list");
            _packageListView.AddToClassList("package-list");
            card.Add(_packageListView);

            var editRow = Row();
            editRow.Add(MakeBtn("删除选中", () =>
            {
                if (_packageListView.selectedIndex < 0 || _packageListView.selectedIndex >= _settings.packageAssetPaths.Count) return;
                _settings.packageAssetPaths.RemoveAt(_packageListView.selectedIndex);
                SaveSettings();
                RefreshAll();
            }, "btn-warn"));
            editRow.Add(MakeBtn("定位选中", () =>
            {
                if (_packageListView.selectedIndex < 0 || _packageListView.selectedIndex >= _settings.packageAssetPaths.Count) return;
                var p = _settings.packageAssetPaths[_packageListView.selectedIndex];
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(p + "_fui.bytes");
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            }, "btn"));
            card.Add(editRow);

            _packageOpResult = new Label();
            _packageOpResult.AddToClassList("hint");
            card.Add(_packageOpResult);
        }

        private void BuildPublishTab()
        {
            var card = Card("发布工作流（推荐顺序：解析路径 -> 发布同步）");
            _contentRoot.Add(card);
            card.Add(MakeInfo("Unity 不能直接运行 .fairy，需先在 FGUI 编辑器中发布，再同步到本插件 Resources 目录（默认见下方目标路径）。"));
            _exeField = AddPathField(card, "FairyGUI编辑器EXE", _settings.fairyGuiEditorExePath, true, ".exe", v => _settings.fairyGuiEditorExePath = v);
            _fairyProjectField = AddFairyProjectField(card, highlightKeyPath: true);

            card.Add(MakeSubTitle("FGUI发布目录（支持三目录）"));
            _publishMainField = AddPathField(card, "发布主目录 publish", _settings.publishMainFolder, false, "", v => _settings.publishMainFolder = v);
            _publishSrcField = AddPathField(card, "源码目录 src", _settings.publishSrcFolder, false, "", v => _settings.publishSrcFolder = v);
            _publishSubpkgField = AddPathField(card, "分包目录 subpkg", _settings.publishSubpkgFolder, false, "", v => _settings.publishSubpkgFolder = v);
            _unityTargetField = AddPathField(card, "Unity目标根目录(Assets下)", _settings.unityTargetRootFolder, false, "", v => _settings.unityTargetRootFolder = v, convertToAssetsRelative: true, highlightKeyPath: true);
            _clearBeforeCopyToggle = new Toggle("同步前清空目标目录") { value = _settings.clearTargetBeforeCopy };
            StyleFormField(_clearBeforeCopyToggle, false);
            _clearBeforeCopyToggle.RegisterValueChangedCallback(v => _settings.clearTargetBeforeCopy = v.newValue);
            card.Add(_clearBeforeCopyToggle);

            var ops = Row();
            ops.AddToClassList("row-wrap");
            ops.Add(MakeBtn("启动FairyGUI编辑器", LaunchExternalEditor, "btn"));
            ops.Add(MakeBtn("解析 .fairy 自定义路径", () => { ParseFairyCustomPaths(); RefreshAll(); }, "btn"));
            ops.Add(MakeBtn("自动推断路径", () => { TryAutoInferPaths(); RefreshAll(); }, "btn"));
            ops.Add(MakeBtn("发布同步 -> Unity", () => { PublishSyncToUnity(); RefreshHealth(); }, "btn-primary"));
            ops.Add(MakeBtn("一键流程", () => { OneClickWorkflow(); RefreshAll(); }, "btn-primary"));
            card.Add(ops);

            _publishResultLabel = new Label("上次同步复制文件数：0");
            _publishResultLabel.AddToClassList("hint");
            card.Add(_publishResultLabel);
        }

        private void BuildQuickPanelTab()
        {
            var card = Card("快速创建/更新 UIPanel");
            _contentRoot.Add(card);
            card.Add(MakeInfo("包与组件仅能通过下拉选择；包列表来自「包管理」中已注册的 *_fui 路径。"));

            if (_settings.packageAssetPaths.Count == 0)
            {
                card.Add(MakeWarn("未注册任何包路径，请先去「包管理」扫描。"));
                return;
            }

            var pkgBlock = FancySelectBlock(
                "包名",
                "下拉仅显示「Unity 发布根目录」之后的相对路径；悬停可看完整 Assets 路径。");
            var pkgIndex = ResolveDefaultPackagePathIndex();
            _packagePathPopup = new PopupField<string>(
                string.Empty,
                _settings.packageAssetPaths,
                pkgIndex,
                FormatPackageRowShort,
                FormatPackageRowShort);
            StyleFancyPopup(_packagePathPopup);
            _packagePathPopup.RegisterValueChangedCallback(v =>
            {
                _settings.defaultPackageAssetPath = v.newValue;
                EnsurePackageLoaded(v.newValue);
                _packagePathPopup.tooltip = v.newValue;
                RebuildQuickComponentPopup(_quickComponentBlock);
                RefreshQuickPanelReadouts();
                RefreshQuickSelectionInfo();
                SaveSettings();
            });
            pkgBlock.Add(_packagePathPopup);
            _quickPackageReadout = new Label();
            _quickPackageReadout.AddToClassList("fancy-select-readout");
            pkgBlock.Add(_quickPackageReadout);
            card.Add(pkgBlock);

            if (_settings.packageAssetPaths.Count > 0)
            {
                var path = _settings.packageAssetPaths[pkgIndex];
                _packagePathPopup.SetValueWithoutNotify(path);
                _packagePathPopup.tooltip = path;
                _settings.defaultPackageAssetPath = path;
            }

            var compBlock = FancySelectBlock(
                "组件名",
                "下拉仅显示组件名；下方汇总 包名/组件。");
            _componentPopup = null;
            compBlock.name = "quick-component-block";
            _quickComponentBlock = compBlock;
            card.Add(compBlock);

            EnsurePackageLoaded(_packagePathPopup.value);
            RebuildQuickComponentPopup(compBlock);

            _quickSelectionInfoLabel = new Label();
            _quickSelectionInfoLabel.AddToClassList("selection-info");
            card.Add(_quickSelectionInfoLabel);
            RefreshQuickPanelReadouts();
            RefreshQuickSelectionInfo();

            var row = Row();
            row.Add(MakeBtn("创建新UIPanel", () =>
            {
                if (!TryGetQuickPanelSelection(out var pkgName, out var compName))
                {
                    EditorUtility.DisplayDialog("FGUI", "请先通过下拉选择有效的包与组件。", "确定");
                    return;
                }
                CreateUIPanel(pkgName, compName);
                SaveSettings();
            }, "btn-primary"));
            row.Add(MakeBtn("应用到当前选中UIPanel", () =>
            {
                if (!TryGetQuickPanelSelection(out var pkgName, out var compName))
                {
                    EditorUtility.DisplayDialog("FGUI", "请先通过下拉选择有效的包与组件。", "确定");
                    return;
                }
                ApplyToSelectedPanel(pkgName, compName);
                SaveSettings();
            }, "btn"));
            card.Add(row);

            EditorApplication.delayCall += () =>
            {
                if (_packagePathPopup != null) _packagePathPopup.MarkDirtyRepaint();
                if (_componentPopup != null) _componentPopup.MarkDirtyRepaint();
            };
        }

        private static VisualElement FancySelectBlock(string title, string hint)
        {
            var block = new VisualElement();
            block.AddToClassList("fancy-select-block");
            var t = new Label(title);
            t.AddToClassList("fancy-select-title");
            block.Add(t);
            var h = new Label(hint);
            h.AddToClassList("fancy-select-hint");
            block.Add(h);
            return block;
        }

        private void StyleFancyPopup(PopupField<string> popup)
        {
            if (popup == null) return;
            popup.AddToClassList("fancy-select-popup");
            popup.AddToClassList("quick-select-dropdown");
        }

        private int ResolveDefaultPackagePathIndex()
        {
            if (_settings.packageAssetPaths == null || _settings.packageAssetPaths.Count == 0) return 0;
            if (!string.IsNullOrWhiteSpace(_settings.defaultPackageAssetPath))
            {
                var idx = _settings.packageAssetPaths.FindIndex(p =>
                    string.Equals(NormalizePath(p), NormalizePath(_settings.defaultPackageAssetPath), StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) return idx;
            }
            return 0;
        }

        private void RebuildQuickComponentPopup(VisualElement compBlock = null)
        {
            compBlock ??= _quickComponentBlock;
            if (compBlock == null) return;

            var readout = _quickComponentReadout;
            if (readout != null && readout.parent == compBlock)
                compBlock.Remove(readout);

            for (var i = compBlock.childCount - 1; i >= 0; i--)
            {
                var c = compBlock[i];
                if (c != null && (c.ClassListContains("fancy-select-popup") || c.ClassListContains("fancy-select-empty")))
                    compBlock.RemoveAt(i);
            }

            RefreshQuickComponentList();
            _componentPopup = null;

            if (_componentNames.Count == 0)
            {
                var empty = new Label("当前包无可用组件，请确认已发布并导入资源。");
                empty.AddToClassList("fancy-select-empty");
                compBlock.Add(empty);
                if (readout == null)
                {
                    readout = new Label();
                    readout.AddToClassList("fancy-select-readout");
                    _quickComponentReadout = readout;
                }
                compBlock.Add(readout);
                RefreshQuickPanelReadouts();
                return;
            }

            var defaultIdx = 0;
            if (!string.IsNullOrWhiteSpace(_settings.defaultComponentName))
            {
                var idx = _componentNames.FindIndex(n =>
                    string.Equals(n, _settings.defaultComponentName, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) defaultIdx = idx;
            }

            _componentPopup = new PopupField<string>(string.Empty, _componentNames, defaultIdx, FormatComponentDisplay, FormatComponentDisplay);
            StyleFancyPopup(_componentPopup);
            _settings.defaultComponentName = _componentNames[defaultIdx];
            _componentPopup.RegisterValueChangedCallback(v =>
            {
                _settings.defaultComponentName = v.newValue;
                RefreshQuickPanelReadouts();
                RefreshQuickSelectionInfo();
                SaveSettings();
            });
            compBlock.Add(_componentPopup);
            if (readout == null)
            {
                readout = new Label();
                readout.AddToClassList("fancy-select-readout");
                _quickComponentReadout = readout;
            }
            compBlock.Add(readout);
            RefreshQuickPanelReadouts();
        }

        private bool TryGetQuickPanelSelection(out string packageName, out string componentName)
        {
            packageName = null;
            componentName = null;
            var path = _packagePathPopup?.value;
            if (string.IsNullOrWhiteSpace(path)) return false;
            var pkg = EnsurePackageLoaded(path);
            if (pkg == null || string.IsNullOrWhiteSpace(pkg.name)) return false;
            packageName = pkg.name;
            if (_componentPopup == null || string.IsNullOrWhiteSpace(_componentPopup.value)) return false;
            componentName = _componentPopup.value;
            return true;
        }

        private string FormatPackageRowShort(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return "(空)";
            var pkg = EnsurePackageLoaded(fullPath);
            var name = pkg != null ? pkg.name : "(未加载)";
            var rel = ToPathAfterUnityPublishRoot(fullPath);
            return $"[{name}]  {rel}";
        }

        private string ToPathAfterUnityPublishRoot(string packageAssetPath)
        {
            if (string.IsNullOrWhiteSpace(packageAssetPath)) return string.Empty;
            var p = NormalizePath(packageAssetPath);
            var candidates = new[]
            {
                NormalizePath(_settings != null ? _settings.unityTargetRootFolder : string.Empty),
                NormalizePath(DefaultUnityPublishRoot)
            };
            foreach (var root in candidates)
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                var prefix = root.TrimEnd('/') + "/";
                if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var tail = p.Substring(prefix.Length);
                    return string.IsNullOrEmpty(tail) ? "." : tail;
                }
            }
            return FormatPackageDisplay(p);
        }

        private void RefreshQuickPanelReadouts()
        {
            if (_quickPackageReadout != null)
            {
                var path = _packagePathPopup?.value;
                if (string.IsNullOrWhiteSpace(path))
                {
                    _quickPackageReadout.text = "当前：未选择包";
                    _quickPackageReadout.tooltip = null;
                }
                else
                {
                    var pkg = EnsurePackageLoaded(path);
                    var n = pkg != null ? pkg.name : "(未加载)";
                    var full = NormalizePath(path);
                    var rel = ToPathAfterUnityPublishRoot(path);
                    _quickPackageReadout.text = $"包「{n}」  ·  发布后相对路径「{rel}」";
                    _quickPackageReadout.tooltip = full;
                }
            }
            if (_quickComponentReadout != null)
            {
                var comp = _componentPopup?.value;
                var path = _packagePathPopup?.value;
                var pkg = string.IsNullOrWhiteSpace(path) ? null : EnsurePackageLoaded(path);
                var pn = pkg != null ? pkg.name : "?";
                if (string.IsNullOrWhiteSpace(comp))
                {
                    _quickComponentReadout.text = "当前：未选择组件";
                    _quickComponentReadout.tooltip = null;
                }
                else
                {
                    _quickComponentReadout.text = $"组件「{comp}」（所属包「{pn}」）";
                    _quickComponentReadout.tooltip = $"{pn}/{comp}";
                }
            }
        }

        private void BuildReferencesTab()
        {
            var card = Card("引用查找");
            _contentRoot.Add(card);

            var row = Row();
            _searchField = new UitkTextField("检索文本") { value = "PetPanel" };
            StyleFormField(_searchField, true);
            row.Add(_searchField);
            row.Add(MakeBtn("查找", SearchNow, "btn-primary"));
            row.Add(MakeBtn("清空", () => _searchResultContainer.Clear(), "btn-warn"));
            card.Add(row);

            _searchResultContainer = new VisualElement();
            _searchResultContainer.AddToClassList("results");
            card.Add(_searchResultContainer);
        }

        private VisualElement Card(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            var t = new Label(title);
            t.AddToClassList("card-title");
            card.Add(t);
            return card;
        }

        private VisualElement Row()
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            return row;
        }

        private Label MakeInfo(string text)
        {
            var l = new Label(text);
            l.AddToClassList("info");
            return l;
        }

        private Label MakeWarn(string text)
        {
            var l = new Label(text);
            l.AddToClassList("warn");
            return l;
        }

        private Label MakeSubTitle(string text)
        {
            var l = new Label(text);
            l.AddToClassList("sub-title");
            return l;
        }

        private Button MakeBtn(string text, Action onClick, string cssClass)
        {
            var btn = new Button(() => onClick?.Invoke()) { text = text };
            btn.AddToClassList("btn");
            btn.AddToClassList(cssClass);
            return btn;
        }

        private UitkTextField AddPathField(VisualElement parent, string label, string value, bool isFile, string ext, Action<string> setter, bool convertToAssetsRelative = false, bool highlightKeyPath = false)
        {
            var row = Row();
            if (highlightKeyPath) row.AddToClassList("publish-path-key");
            var tf = new UitkTextField(label) { value = value ?? string.Empty };
            StyleFormField(tf, true);
            tf.RegisterValueChangedCallback(v => setter(NormalizePath(v.newValue)));
            row.Add(tf);
            var browse = MakeBtn("浏览", () =>
            {
                var selected = isFile
                    ? EditorUtility.OpenFilePanel($"选择{label}", GetParentFolder(tf.value), ext.TrimStart('.'))
                    : EditorUtility.OpenFolderPanel($"选择{label}", GetParentFolder(tf.value), string.Empty);
                if (string.IsNullOrWhiteSpace(selected)) return;
                selected = NormalizePath(selected);
                if (convertToAssetsRelative)
                    selected = ToAssetsRelativePath(selected, tf.value);
                tf.value = selected;
            }, "btn");
            row.Add(browse);
            parent.Add(row);
            return tf;
        }

        private UitkTextField AddFairyProjectField(VisualElement parent, bool highlightKeyPath = false)
        {
            var row = Row();
            if (highlightKeyPath) row.AddToClassList("publish-path-key");
            var tf = new UitkTextField("FGUI工程路径 (.fairy)") { value = _settings.fairyGuiProjectPath ?? string.Empty };
            StyleFormField(tf, true);
            tf.RegisterValueChangedCallback(v => _settings.fairyGuiProjectPath = NormalizePath(v.newValue));
            row.Add(tf);
            row.Add(MakeBtn("选.fairy", () =>
            {
                var f = EditorUtility.OpenFilePanel("选择 .fairy 工程文件", GetParentFolder(tf.value), "fairy");
                if (!string.IsNullOrWhiteSpace(f)) tf.value = NormalizePath(f);
            }, "btn"));
            row.Add(MakeBtn("选目录", () =>
            {
                var d = EditorUtility.OpenFolderPanel("选择 FGUI 工程目录", GetParentFolder(tf.value), string.Empty);
                if (!string.IsNullOrWhiteSpace(d)) tf.value = NormalizePath(d);
            }, "btn"));
            parent.Add(row);
            return tf;
        }

        private static void StyleFormField(VisualElement field, bool grow)
        {
            if (field == null) return;
            field.AddToClassList("form-field");
            if (grow) field.AddToClassList("field-grow");
        }

        private static string FormatPackageDisplay(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return "(空)";
            var p = NormalizePath(fullPath);
            var parts = p.Split('/');
            if (parts.Length <= 2) return p;
            return $"{parts[parts.Length - 2]}/{parts[parts.Length - 1]}";
        }

        private static string FormatComponentDisplay(string componentName)
        {
            if (string.IsNullOrWhiteSpace(componentName)) return "(空)";
            return componentName;
        }

        private void RefreshQuickSelectionInfo()
        {
            RefreshQuickPanelReadouts();
            if (_quickSelectionInfoLabel == null) return;
            var packagePath = _packagePathPopup?.value ?? "(未选)";
            string packageName = "(未加载)";
            if (!string.IsNullOrWhiteSpace(_packagePathPopup?.value))
            {
                var pkg = EnsurePackageLoaded(_packagePathPopup.value);
                if (pkg != null && !string.IsNullOrWhiteSpace(pkg.name)) packageName = pkg.name;
            }
            var component = _componentPopup != null ? _componentPopup.value : null;
            if (string.IsNullOrWhiteSpace(component)) component = "(未选)";
            var rel = string.IsNullOrWhiteSpace(_packagePathPopup?.value)
                ? "(未选)"
                : ToPathAfterUnityPublishRoot(packagePath);
            _quickSelectionInfoLabel.text = $"汇总  发布后相对: {rel}  |  包: {packageName}  |  组件: {component}";
            _quickSelectionInfoLabel.tooltip = string.IsNullOrWhiteSpace(_packagePathPopup?.value)
                ? null
                : $"{NormalizePath(packagePath)}  →  {packageName}/{component}";
        }

        private void RefreshAll()
        {
            RefreshHealth();
            RebuildTabContent();
            RefreshTabButtonStyles();
        }

        private void RefreshHealth()
        {
            if (_healthLabel == null || _healthBar == null || _settings == null) return;
            int state = EvaluateHealth(out var detail);
            _healthLabel.text = $"设置健康状态：{(state == 2 ? "正常" : state == 1 ? "警告" : "异常")} | {detail}";
            _healthBar.EnableInClassList("health-good", state == 2);
            _healthBar.EnableInClassList("health-warn", state == 1);
            _healthBar.EnableInClassList("health-bad", state == 0);
        }

        private int EvaluateHealth(out string detail)
        {
            bool soOk = AssetDatabase.LoadAssetAtPath<FguiWorkflowSettings>(SettingsAssetPath) != null;
            bool backupOk = File.Exists(Path.GetFullPath(SettingsBackupJsonPath));
            bool fairyOk = !string.IsNullOrWhiteSpace(_settings.fairyGuiProjectPath) &&
                           (File.Exists(_settings.fairyGuiProjectPath) || Directory.Exists(_settings.fairyGuiProjectPath));
            int publishCount = 0;
            if (!string.IsNullOrWhiteSpace(_settings.publishMainFolder) && Directory.Exists(_settings.publishMainFolder)) publishCount++;
            if (!string.IsNullOrWhiteSpace(_settings.publishSrcFolder) && Directory.Exists(_settings.publishSrcFolder)) publishCount++;
            if (!string.IsNullOrWhiteSpace(_settings.publishSubpkgFolder) && Directory.Exists(_settings.publishSubpkgFolder)) publishCount++;
            bool targetOk = !string.IsNullOrWhiteSpace(_settings.unityTargetRootFolder) &&
                            (_settings.unityTargetRootFolder.Equals("Assets", StringComparison.OrdinalIgnoreCase) ||
                             _settings.unityTargetRootFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase));

            detail = $"SO:{(soOk ? "OK" : "缺失")} | 备份:{(backupOk ? "OK" : "缺失")} | 工程:{(fairyOk ? "OK" : "未设置")} | 发布目录:{publishCount}/3 | 目标:{(targetOk ? "OK" : "非法")}";
            if (!soOk || !targetOk) return 0;
            if (!backupOk || !fairyOk || publishCount == 0) return 1;
            return 2;
        }

        private void SearchNow()
        {
            _searchResultContainer.Clear();
            var hits = new List<SearchHit>();
            FindReferences(_searchField.value, hits);
            if (hits.Count == 0)
            {
                _searchResultContainer.Add(MakeWarn("未找到匹配内容。"));
                return;
            }

            foreach (var hit in hits)
            {
                var row = new VisualElement();
                row.AddToClassList("result-item");
                row.Add(new Label($"{hit.assetPath}:{hit.line}") { tooltip = hit.preview });
                row.Add(MakeBtn("打开", () =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(hit.assetPath);
                    if (asset != null)
                    {
                        AssetDatabase.OpenAsset(asset, Math.Max(1, hit.line));
                        Selection.activeObject = asset;
                    }
                }, "btn"));
                _searchResultContainer.Add(row);
            }
        }

        private void DiscoverPackages()
        {
            _settings.packageAssetPaths.Clear();
            var guids = AssetDatabase.FindAssets("*_fui t:TextAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("_fui.bytes", StringComparison.OrdinalIgnoreCase)) continue;
                var basePath = path.Substring(0, path.Length - "_fui.bytes".Length);
                _settings.packageAssetPaths.Add(basePath);
            }
            NormalizePackageList();
            SaveSettings();
            RefreshAll();
        }

        private void AddPackageByBrowse()
        {
            var file = EditorUtility.OpenFilePanel("选择包描述文件(*_fui.bytes)", "Assets", "bytes");
            if (string.IsNullOrWhiteSpace(file)) return;
            file = NormalizePath(file);
            var projectRoot = NormalizePath(Directory.GetCurrentDirectory());
            if (!file.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("FGUI", "请选择 Unity 工程内的文件。", "确定");
                return;
            }
            var relative = file.Substring(projectRoot.Length + 1);
            if (!relative.EndsWith("_fui.bytes", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("FGUI", "所选文件不是 *_fui.bytes。", "确定");
                return;
            }
            var basePath = relative.Substring(0, relative.Length - "_fui.bytes".Length);
            if (!_settings.packageAssetPaths.Contains(basePath))
                _settings.packageAssetPaths.Add(basePath);
            NormalizePackageList();
            SaveSettings();
            RefreshAll();
        }

        private void NormalizePackageList()
        {
            _settings.packageAssetPaths = _settings.packageAssetPaths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(NormalizePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private FGUIPackage EnsurePackageLoaded(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath)) return null;
            try
            {
                var exists = FGUIPackage.GetById(packagePath);
                if (exists != null) return exists;
                return FGUIPackage.AddPackage(packagePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EnsurePackageLoaded failed: {packagePath}\n{e.Message}");
                return null;
            }
        }

        private void RefreshQuickComponentList()
        {
            _componentNames.Clear();
            var packagePath = _packagePathPopup != null ? _packagePathPopup.value : _settings.packageAssetPaths.FirstOrDefault();
            var pkg = EnsurePackageLoaded(packagePath);
            if (pkg == null) return;

            foreach (var item in pkg.GetItems())
            {
                if (item == null || item.type != FGUIPackageItemType.Component) continue;
                _componentNames.Add(item.name);
            }
            _componentNames.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private void LoadAllRegistered()
        {
            foreach (var packagePath in _settings.packageAssetPaths)
            {
                EnsurePackageLoaded(packagePath);
            }
            if (_packageOpResult != null) _packageOpResult.text = "已尝试加载全部注册包。";
        }

        private void UnloadAllRegistered()
        {
            foreach (var packagePath in _settings.packageAssetPaths)
            {
                try { FGUIPackage.RemovePackage(packagePath); }
                catch { /* ignore */ }
            }
            if (_packageOpResult != null) _packageOpResult.text = "已尝试卸载全部注册包。";
        }

        private void PublishSyncToUnity()
        {
            _copyFileCount = 0;
            if (string.IsNullOrWhiteSpace(_settings.unityTargetRootFolder) || !_settings.unityTargetRootFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("FGUI发布", "Unity目标目录必须是 Assets/... 路径。", "确定");
                return;
            }

            var sourceDirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(_settings.publishMainFolder) && Directory.Exists(_settings.publishMainFolder)) sourceDirs.Add(_settings.publishMainFolder);
            if (!string.IsNullOrWhiteSpace(_settings.publishSrcFolder) && Directory.Exists(_settings.publishSrcFolder)) sourceDirs.Add(_settings.publishSrcFolder);
            if (!string.IsNullOrWhiteSpace(_settings.publishSubpkgFolder) && Directory.Exists(_settings.publishSubpkgFolder)) sourceDirs.Add(_settings.publishSubpkgFolder);
            if (sourceDirs.Count == 0)
            {
                EditorUtility.DisplayDialog("FGUI发布", "请至少配置一个有效发布目录（publish/src/subpkg）。", "确定");
                return;
            }

            EnsureFolder(_settings.unityTargetRootFolder);
            var projectRoot = NormalizePath(Directory.GetCurrentDirectory());
            var targetRoot = NormalizePath(Path.Combine(projectRoot, _settings.unityTargetRootFolder));
            if (!Directory.Exists(targetRoot)) Directory.CreateDirectory(targetRoot);

            if (_settings.clearTargetBeforeCopy && Directory.Exists(targetRoot))
            {
                foreach (var file in Directory.GetFiles(targetRoot, "*.*", SearchOption.AllDirectories))
                    File.Delete(file);
            }

            foreach (var srcDir in sourceDirs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var srcRootNorm = NormalizePath(srcDir);
                var rootName = Path.GetFileName(srcRootNorm.TrimEnd('/'));
                foreach (var srcFile in Directory.EnumerateFiles(srcRootNorm, "*.*", SearchOption.AllDirectories))
                {
                    var srcNorm = NormalizePath(srcFile);
                    if (srcNorm.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    var rel = srcNorm.Substring(srcRootNorm.Length).TrimStart('/');
                    var dest = NormalizePath(Path.Combine(targetRoot, rootName, rel));
                    var destDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrWhiteSpace(destDir) && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);
                    File.Copy(srcNorm, dest, true);
                    _copyFileCount++;
                }
            }

            AssetDatabase.Refresh();
            if (_publishResultLabel != null) _publishResultLabel.text = $"上次同步复制文件数：{_copyFileCount}";
            RefreshHealth();
        }

        private void TryAutoInferPaths()
        {
            var p = _settings.fairyGuiProjectPath;
            if (string.IsNullOrWhiteSpace(p)) return;
            p = NormalizePath(p);

            string rootDir = null;
            if (File.Exists(p) && p.EndsWith(".fairy", StringComparison.OrdinalIgnoreCase))
                rootDir = NormalizePath(Path.GetDirectoryName(p));
            else if (Directory.Exists(p))
                rootDir = p;

            if (string.IsNullOrWhiteSpace(rootDir)) return;

            var publish = NormalizePath(Path.Combine(rootDir, "publish"));
            var src = NormalizePath(Path.Combine(rootDir, "src"));
            var subpkg = NormalizePath(Path.Combine(rootDir, "subpkg"));
            if (Directory.Exists(publish)) _settings.publishMainFolder = publish;
            if (Directory.Exists(src)) _settings.publishSrcFolder = src;
            if (Directory.Exists(subpkg)) _settings.publishSubpkgFolder = subpkg;
            if (string.IsNullOrWhiteSpace(_settings.unityTargetRootFolder))
                _settings.unityTargetRootFolder = DefaultUnityPublishRoot;

            ParseFairyCustomPaths();
        }

        private void OneClickWorkflow()
        {
            TryAutoInferPaths();
            PublishSyncToUnity();
            DiscoverPackages();
            LoadAllRegistered();
            RefreshAll();
        }

        private void ParseFairyCustomPaths()
        {
            var fairyFile = ResolveFairyFile(_settings.fairyGuiProjectPath);
            if (string.IsNullOrWhiteSpace(fairyFile) || !File.Exists(fairyFile))
                return;

            var rootDir = NormalizePath(Path.GetDirectoryName(fairyFile));
            var settingsDir = NormalizePath(Path.Combine(rootDir, "settings"));

            string publishVal = null, srcVal = null, subpkgVal = null;
            var customProps = NormalizePath(Path.Combine(settingsDir, "CustomProperties.json"));
            if (File.Exists(customProps))
            {
                var customJson = File.ReadAllText(customProps);
                publishVal = TryExtractCustomProp(customJson, "publish");
                srcVal = TryExtractCustomProp(customJson, "src");
                subpkgVal = TryExtractCustomProp(customJson, "subpkg");
            }

            if (string.IsNullOrWhiteSpace(publishVal) || string.IsNullOrWhiteSpace(srcVal) || string.IsNullOrWhiteSpace(subpkgVal))
            {
                var fairyContent = File.ReadAllText(fairyFile);
                publishVal ??= TryExtractCustomProp(fairyContent, "publish");
                srcVal ??= TryExtractCustomProp(fairyContent, "src");
                subpkgVal ??= TryExtractCustomProp(fairyContent, "subpkg");
            }

            if (!string.IsNullOrWhiteSpace(publishVal)) _settings.publishMainFolder = ToAbsoluteDir(rootDir, publishVal);
            if (!string.IsNullOrWhiteSpace(srcVal)) _settings.publishSrcFolder = ToAbsoluteDir(rootDir, srcVal);
            if (!string.IsNullOrWhiteSpace(subpkgVal)) _settings.publishSubpkgFolder = ToAbsoluteDir(rootDir, subpkgVal);
        }

        private static string ResolveFairyFile(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath)) return null;
            var p = NormalizePath(projectPath);
            if (File.Exists(p) && p.EndsWith(".fairy", StringComparison.OrdinalIgnoreCase)) return p;
            if (Directory.Exists(p))
            {
                var files = Directory.GetFiles(p, "*.fairy", SearchOption.TopDirectoryOnly);
                if (files.Length > 0) return NormalizePath(files[0]);
            }
            return null;
        }

        private static string TryExtractCustomProp(string content, string key)
        {
            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(key)) return null;
            var patternJson = $"\"{Regex.Escape(key)}\"\\s*:\\s*\"(?<v>[^\"]+)\"";
            var m = Regex.Match(content, patternJson, RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups["v"].Value;
            var patternEq = $"{Regex.Escape(key)}\\s*=\\s*(?<v>[^\\r\\n;]+)";
            m = Regex.Match(content, patternEq, RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["v"].Value.Trim() : null;
        }

        private static string ToAbsoluteDir(string rootDir, string val)
        {
            var p = NormalizePath(val.Trim());
            if (Path.IsPathRooted(p)) return p;
            return NormalizePath(Path.Combine(rootDir, p));
        }

        private void LaunchExternalEditor()
        {
            if (string.IsNullOrWhiteSpace(_settings.fairyGuiEditorExePath))
            {
                EditorUtility.DisplayDialog("FGUI", "请先设置 FairyGUI 编辑器 EXE 路径。", "确定");
                return;
            }
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _settings.fairyGuiEditorExePath,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(_settings.fairyGuiProjectPath))
                psi.Arguments = $"\"{_settings.fairyGuiProjectPath}\"";
            try { System.Diagnostics.Process.Start(psi); }
            catch (Exception e) { Debug.LogError($"启动 FairyGUI 编辑器失败：{e.Message}"); }
        }

        private static void CreateUIPanel(string packageName, string componentName)
        {
            var go = new GameObject(string.IsNullOrWhiteSpace(componentName) ? "UIPanel_FGUI" : $"UIPanel_{componentName}");
            var panel = go.AddComponent<FGUIPanel>();
            panel.packageName = packageName;
            panel.componentName = componentName;
            panel.sortingOrder = 100;
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
            Undo.RegisterCreatedObjectUndo(go, "Create FGUI UIPanel");
        }

        private static void ApplyToSelectedPanel(string packageName, string componentName)
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("FGUI", "请先选中带 UIPanel 的对象。", "确定");
                return;
            }
            var panel = go.GetComponent<FGUIPanel>();
            if (panel == null)
            {
                EditorUtility.DisplayDialog("FGUI", "当前选中对象不含 UIPanel。", "确定");
                return;
            }
            Undo.RecordObject(panel, "Apply FGUI UIPanel Data");
            panel.packageName = packageName;
            panel.componentName = componentName;
            EditorUtility.SetDirty(panel);
        }

        private void FindReferences(string query, List<SearchHit> output)
        {
            output.Clear();
            if (string.IsNullOrWhiteSpace(query)) return;
            string[] exts = { ".cs", ".prefab", ".unity", ".txt", ".bytes" };
            foreach (var assetPath in EnumerateProjectFiles("Assets"))
            {
                if (!HasExt(assetPath, exts)) continue;
                if (ShouldSkipReferenceSearch(assetPath)) continue;
                TrySearchFile(assetPath, query, output);
            }
        }

        private static bool ShouldSkipReferenceSearch(string assetPath)
        {
            if (assetPath.StartsWith("Assets/FGUIforUnity/Scripts/", StringComparison.OrdinalIgnoreCase)) return true;
            if (assetPath.StartsWith("Assets/Game/UI/FGUI/Resources/Shaders/", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static IEnumerable<string> EnumerateProjectFiles(string root)
        {
            var fullRoot = Path.GetFullPath(root);
            foreach (var file in Directory.EnumerateFiles(fullRoot, "*.*", SearchOption.AllDirectories))
            {
                var normalized = file.Replace('\\', '/');
                int idx = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;
                yield return normalized.Substring(idx + 1);
            }
        }

        private static bool HasExt(string path, string[] exts)
        {
            for (int i = 0; i < exts.Length; i++)
                if (path.EndsWith(exts[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void TrySearchFile(string assetPath, string query, List<SearchHit> output)
        {
            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) return;
            string[] lines;
            try { lines = File.ReadAllLines(fullPath); }
            catch { return; }

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                output.Add(new SearchHit
                {
                    assetPath = assetPath,
                    line = i + 1,
                    preview = lines[i].Trim().Length > 240 ? lines[i].Trim().Substring(0, 240) : lines[i].Trim()
                });
            }
        }

        private void SaveSettings()
        {
            if (_settings == null) return;
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            WriteSettingsBackupJson();
            RefreshHealth();
        }

        [Serializable]
        private sealed class SettingsBackupDto
        {
            public string fairyGuiEditorExePath;
            public string fairyGuiProjectPath;
            public string publishMainFolder;
            public string publishSrcFolder;
            public string publishSubpkgFolder;
            public string unityTargetRootFolder;
            public bool clearTargetBeforeCopy;
            public List<string> packageAssetPaths = new List<string>();
            public string defaultPackageAssetPath;
            public string defaultComponentName;
            public string defaultPackageName;
        }

        private void WriteSettingsBackupJson()
        {
            if (_settings == null) return;
            var dto = new SettingsBackupDto
            {
                fairyGuiEditorExePath = _settings.fairyGuiEditorExePath,
                fairyGuiProjectPath = _settings.fairyGuiProjectPath,
                publishMainFolder = _settings.publishMainFolder,
                publishSrcFolder = _settings.publishSrcFolder,
                publishSubpkgFolder = _settings.publishSubpkgFolder,
                unityTargetRootFolder = _settings.unityTargetRootFolder,
                clearTargetBeforeCopy = _settings.clearTargetBeforeCopy,
                packageAssetPaths = _settings.packageAssetPaths != null ? new List<string>(_settings.packageAssetPaths) : new List<string>(),
                defaultPackageAssetPath = _settings.defaultPackageAssetPath,
                defaultComponentName = _settings.defaultComponentName,
            };
            var json = JsonUtility.ToJson(dto, true);
            File.WriteAllText(Path.GetFullPath(SettingsBackupJsonPath), json, Encoding.UTF8);
        }

        private static FguiWorkflowSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<FguiWorkflowSettings>(SettingsAssetPath);
            if (settings != null)
            {
                ApplyBackupIfNeeded(settings, false);
                return settings;
            }

            EnsureFolder("Assets/FGUIforUnity/Editor/Workflow");
            settings = ScriptableObject.CreateInstance<FguiWorkflowSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            ApplyBackupIfNeeded(settings, false);
            return settings;
        }

        private static void ApplyBackupIfNeeded(FguiWorkflowSettings settings, bool forceOverride)
        {
            try
            {
                var backupPath = Path.GetFullPath(SettingsBackupJsonPath);
                if (!File.Exists(backupPath)) return;
                var json = File.ReadAllText(backupPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return;
                var dto = JsonUtility.FromJson<SettingsBackupDto>(json);
                if (dto == null) return;

                bool dirty = false;
                dirty |= FillIfEmptyOrOverride(ref settings.fairyGuiEditorExePath, dto.fairyGuiEditorExePath, forceOverride);
                dirty |= FillIfEmptyOrOverride(ref settings.fairyGuiProjectPath, dto.fairyGuiProjectPath, forceOverride);
                dirty |= FillIfEmptyOrOverride(ref settings.publishMainFolder, dto.publishMainFolder, forceOverride);
                dirty |= FillIfEmptyOrOverride(ref settings.publishSrcFolder, dto.publishSrcFolder, forceOverride);
                dirty |= FillIfEmptyOrOverride(ref settings.publishSubpkgFolder, dto.publishSubpkgFolder, forceOverride);
                dirty |= FillIfEmptyOrOverride(ref settings.unityTargetRootFolder, dto.unityTargetRootFolder, forceOverride);
                if (forceOverride || settings.packageAssetPaths == null || settings.packageAssetPaths.Count == 0)
                {
                    settings.packageAssetPaths = dto.packageAssetPaths ?? new List<string>();
                    dirty = true;
                }
                dirty |= FillIfEmptyOrOverride(ref settings.defaultPackageAssetPath, dto.defaultPackageAssetPath, forceOverride);
                dirty |= FillIfEmptyOrOverride(ref settings.defaultComponentName, dto.defaultComponentName, forceOverride);
                if (string.IsNullOrWhiteSpace(settings.defaultPackageAssetPath) &&
                    !string.IsNullOrWhiteSpace(dto.defaultPackageName) &&
                    settings.packageAssetPaths != null)
                {
                    var legacyName = dto.defaultPackageName.Trim();
                    var match = settings.packageAssetPaths.FirstOrDefault(p =>
                    {
                        var seg = p.TrimEnd('/').Split('/');
                        return seg.Length > 0 && seg[seg.Length - 1].Equals(legacyName, StringComparison.OrdinalIgnoreCase);
                    });
                    if (!string.IsNullOrWhiteSpace(match))
                    {
                        settings.defaultPackageAssetPath = match;
                        dirty = true;
                    }
                }
                if (dirty)
                {
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
            }
            catch
            {
                // ignore
            }
        }

        private static bool FillIfEmptyOrOverride(ref string target, string from, bool force)
        {
            if (string.IsNullOrWhiteSpace(from)) return false;
            if (!force && !string.IsNullOrWhiteSpace(target)) return false;
            if (target == from) return false;
            target = from;
            return true;
        }

        private static string ToAssetsRelativePath(string anyPath, string fallback)
        {
            if (string.IsNullOrWhiteSpace(anyPath)) return fallback;
            var p = NormalizePath(anyPath);
            var projectRoot = NormalizePath(Directory.GetCurrentDirectory());
            if (!p.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)) return fallback;
            var rel = p.Substring(projectRoot.Length).TrimStart('/');
            if (!rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && !rel.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                return fallback;
            return rel;
        }

        private static string GetParentFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return Directory.GetCurrentDirectory();
            try
            {
                if (Directory.Exists(path)) return path;
                var dir = Path.GetDirectoryName(path);
                return string.IsNullOrWhiteSpace(dir) ? Directory.GetCurrentDirectory() : dir;
            }
            catch
            {
                return Directory.GetCurrentDirectory();
            }
        }

        private static string NormalizePath(string path) => string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private void ApplySafeEditorBuiltinFont()
        {
            try
            {
                // Use Unity editor built-in font only; avoid project custom fonts that may leave
                // stale TextCore FontAsset references after reimport/domain reload.
                var editorFont = EditorStyles.standardFont != null ? EditorStyles.standardFont : (EditorStyles.label != null ? EditorStyles.label.font : null);
                if (IsFontUsable(editorFont))
                    rootVisualElement.style.unityFontDefinition = FontDefinition.FromFont(editorFont);
            }
            catch
            {
                // Keep Unity default behavior when font assignment fails.
            }
        }

        private static bool IsFontUsable(Font font)
        {
            if (font == null) return false;
            try
            {
                _ = font.name;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

