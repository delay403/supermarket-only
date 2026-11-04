using System.Globalization;
using Game.Core.Config;
using Game.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Debugging
{
    /// <summary>
    /// 装备属性调试：将 UI 绑定到 <see cref="EquipmentStatDebugState"/>。
    /// 推荐在场景中自行搭 UI，关闭 <see cref="rebuildUiFromCode"/> 后在 Inspector 里拖引用。
    /// 若 <see cref="GameBalanceConfig.debugEnableEquipmentStatPanel"/> 未开启，运行时将 <see cref="Destroy"/> 本物体（整块调试 UI 一并移除）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentStatDebugOverlay : MonoBehaviour
    {
        [Tooltip("勾选时运行时用代码动态创建 UI（旧行为）。关闭时请使用下方 Inspector 引用.")]
        [SerializeField] private bool rebuildUiFromCode = false;

        [Header("手动 UI（rebuildUiFromCode 关闭时使用）")]
        [Tooltip("主设置面板 GameObject（打开/关闭时 SetActive）。")]
        [SerializeField] private GameObject settingsPanelRoot;

        [Tooltip("例如层级里的 EquipmentDebugLauncher 上的 Button。")]
        [SerializeField] private Button openPanelButton;

        [Tooltip("面板内关闭按钮；可空（仅能通过外部逻辑关）。")]
        [SerializeField] private Button closePanelButton;

        [SerializeField] private Toggle statOverrideToggle;

        [SerializeField] private InputField inputWingClimbSpeedMul;
        [SerializeField] private InputField inputCharacterCoinsMul;
        [SerializeField] private InputField inputCharacterClimbSpeedMul;
        [SerializeField] private InputField inputTrailCoinsMul;
        [SerializeField] private InputField inputPetSlot1CoinsAdd;
        [SerializeField] private InputField inputPetSlot2CoinsAdd;
        [SerializeField] private InputField inputPetSlot3CoinsAdd;

        RectTransform _panelRt;

        Toggle _overrideToggle;
        InputField _wingMul;
        InputField _charCoinsMul;
        InputField _charClimbMul;
        InputField _trailCoinsMul;
        InputField _pet1Add;
        InputField _pet2Add;
        InputField _pet3Add;

        bool _didInit;

        void Start()
        {
            if (_didInit)
                return;
            _didInit = true;

            var bal = ConfigRegistry.Balance;
            if (bal == null || !bal.debugEnableEquipmentStatPanel)
            {
                Destroy(gameObject);
                return;
            }

            if (rebuildUiFromCode)
                BuildUi();
            else
            {
                WireInspectorBindings();
                if (settingsPanelRoot == null)
                    Debug.LogWarning(
                        $"{nameof(EquipmentStatDebugOverlay)}: 未指定 {nameof(settingsPanelRoot)}，无法打开/关闭调试面板。");
                if (openPanelButton == null)
                    Debug.LogWarning(
                        $"{nameof(EquipmentStatDebugOverlay)}: 未指定 {nameof(openPanelButton)}，将无法从按钮打开面板。");
            }

            SyncFieldsFromState();
            SetPanelVisible(false);
        }

        void WireInspectorBindings()
        {
            _overrideToggle = statOverrideToggle;
            _wingMul = inputWingClimbSpeedMul;
            _charCoinsMul = inputCharacterCoinsMul;
            _charClimbMul = inputCharacterClimbSpeedMul;
            _trailCoinsMul = inputTrailCoinsMul;
            _pet1Add = inputPetSlot1CoinsAdd;
            _pet2Add = inputPetSlot2CoinsAdd;
            _pet3Add = inputPetSlot3CoinsAdd;

            if (openPanelButton != null)
                openPanelButton.onClick.AddListener(() => SetPanelVisible(true));
            if (closePanelButton != null)
                closePanelButton.onClick.AddListener(() => SetPanelVisible(false));

            if (statOverrideToggle != null)
                statOverrideToggle.onValueChanged.AddListener(OnOverrideToggled);

            HookField(_wingMul, v => EquipmentStatDebugState.WingClimbSpeedMul = v);
            HookField(_charCoinsMul, v => EquipmentStatDebugState.CharacterCoinsMul = v);
            HookField(_charClimbMul, v => EquipmentStatDebugState.CharacterClimbSpeedMul = v);
            HookField(_trailCoinsMul, v => EquipmentStatDebugState.TrailCoinsMul = v);
            HookField(_pet1Add, v => EquipmentStatDebugState.PetSlot1CoinsAdd = v);
            HookField(_pet2Add, v => EquipmentStatDebugState.PetSlot2CoinsAdd = v);
            HookField(_pet3Add, v => EquipmentStatDebugState.PetSlot3CoinsAdd = v);

            EnsureTtKeyboardForAllBoundInputs();
        }

        void SetPanelVisible(bool visible)
        {
            var go = settingsPanelRoot != null ? settingsPanelRoot : _panelRt != null ? _panelRt.gameObject : null;
            if (go != null)
                go.SetActive(visible);
        }

        void EnsureTtKeyboardForAllBoundInputs()
        {
            TtMiniGameKeyboardInputField.EnsureOn(_wingMul);
            TtMiniGameKeyboardInputField.EnsureOn(_charCoinsMul);
            TtMiniGameKeyboardInputField.EnsureOn(_charClimbMul);
            TtMiniGameKeyboardInputField.EnsureOn(_trailCoinsMul);
            TtMiniGameKeyboardInputField.EnsureOn(_pet1Add);
            TtMiniGameKeyboardInputField.EnsureOn(_pet2Add);
            TtMiniGameKeyboardInputField.EnsureOn(_pet3Add);
        }

        void BuildLauncherButton()
        {
            var launcher = new GameObject("EquipmentDebugLauncher", typeof(RectTransform));
            var launcherRt = launcher.GetComponent<RectTransform>();
            launcherRt.SetParent(transform, false);
            launcherRt.anchorMin = new Vector2(1f, 1f);
            launcherRt.anchorMax = new Vector2(1f, 1f);
            launcherRt.pivot = new Vector2(1f, 1f);
            launcherRt.anchoredPosition = new Vector2(-12f, -12f);
            launcherRt.sizeDelta = new Vector2(140f, 42f);

            var bg = launcher.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.42f, 0.62f, 0.95f);

            var btn = launcher.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.2f, 0.55f, 0.78f, 1f);
            colors.pressedColor = new Color(0.08f, 0.3f, 0.48f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => SetPanelVisible(true));

            var textGo = new GameObject("Label", typeof(RectTransform));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(launcherRt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
                label.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
            label.text = "装备调试";
            label.fontSize = 17;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
        }

        static Button CreateSmallButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 88f;
            le.preferredHeight = 36f;
            le.minWidth = 88f;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.32f, 0.32f, 0.38f, 1f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            var tGo = new GameObject("Text", typeof(RectTransform));
            var tRt = tGo.GetComponent<RectTransform>();
            tRt.SetParent(rt, false);
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            var t = tGo.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null)
                t.font = Font.CreateDynamicFontFromOSFont("Arial", 15);
            t.text = label;
            t.fontSize = 15;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;

            return btn;
        }

        void BuildHeaderRow(RectTransform panelRt)
        {
            var row = new GameObject("HeaderRow", typeof(RectTransform));
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.SetParent(panelRt, false);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlHeight = true;
            h.childForceExpandHeight = false;
            h.childControlWidth = true;
            h.childForceExpandWidth = true;
            h.padding = new RectOffset(0, 0, 0, 4);

            var title = CreateText(rowRt, "装备属性调试", 19, FontStyle.Bold, TextAnchor.MiddleLeft);
            var leTitle = title.gameObject.GetComponent<LayoutElement>();
            leTitle.flexibleWidth = 1f;
            leTitle.minHeight = 40f;

            CreateSmallButton(rowRt, "关闭", () => SetPanelVisible(false));
        }

        void OnDestroy()
        {
            EquipmentStatDebugState.OverrideEnabled = false;
        }

        void BuildUi()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
            }

            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            BuildLauncherButton();

            var panelRt = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            _panelRt = panelRt;
            panelRt.SetParent(transform, false);
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(12f, -12f);
            panelRt.sizeDelta = new Vector2(420f, 620f);

            var panelBg = panelRt.gameObject.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

            var vlg = panelRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 12);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;

            BuildHeaderRow(panelRt);

            var subtitle = CreateText(panelRt, "StatsAggregator 覆盖 · 仅调试 SO 开启时显示入口", 14,
                FontStyle.Normal, TextAnchor.UpperLeft);
            subtitle.color = new Color(0.75f, 0.78f, 0.85f, 1f);
            var leSub = subtitle.gameObject.AddComponent<LayoutElement>();
            leSub.minHeight = 22f;

            _overrideToggle = CreateToggle(panelRt, "启用覆盖（下方数值将替换对应装备项参与 Σ 合并）");

            AddHint(panelRt,
                "说明：合并公式不变。(base+Σadd)×max(0.95,(1+ΣclimbMul)-地图修正)；(基础金币+ΣcoinsAdd)×(1+ΣcoinsMul+地图修正)×距离÷除数。\n仅替换列出的维度，其余仍读装备表。");

            _wingMul = CreateLabeledField(panelRt, "翅膀 · climbSpeedMul（速度加成倍率）", "例：0.1 表示 +10% 速参与 Σ");
            _charCoinsMul = CreateLabeledField(panelRt, "角色 · coinsMul（金币加成倍率）", "参与 ΣcoinsMul");
            _charClimbMul = CreateLabeledField(panelRt, "角色 · climbSpeedMul（速度加成倍率）", "参与 ΣclimbSpeedMul");
            _trailCoinsMul = CreateLabeledField(panelRt, "拖尾 · coinsMul（金币加成倍率）", "参与 ΣcoinsMul");
            _pet1Add = CreateLabeledField(panelRt, "宠物栏1 · coinsAdd", "替换该栏已装备宠物的 coinsAdd");
            _pet2Add = CreateLabeledField(panelRt, "宠物栏2 · coinsAdd", "同上");
            _pet3Add = CreateLabeledField(panelRt, "宠物栏3 · coinsAdd", "同上");

            _overrideToggle.onValueChanged.AddListener(OnOverrideToggled);
            HookField(_wingMul, v => EquipmentStatDebugState.WingClimbSpeedMul = v);
            HookField(_charCoinsMul, v => EquipmentStatDebugState.CharacterCoinsMul = v);
            HookField(_charClimbMul, v => EquipmentStatDebugState.CharacterClimbSpeedMul = v);
            HookField(_trailCoinsMul, v => EquipmentStatDebugState.TrailCoinsMul = v);
            HookField(_pet1Add, v => EquipmentStatDebugState.PetSlot1CoinsAdd = v);
            HookField(_pet2Add, v => EquipmentStatDebugState.PetSlot2CoinsAdd = v);
            HookField(_pet3Add, v => EquipmentStatDebugState.PetSlot3CoinsAdd = v);

            EnsureTtKeyboardForAllBoundInputs();
        }

        static void AddHint(RectTransform parent, string text)
        {
            var hint = CreateText(parent, text, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            hint.color = new Color(0.75f, 0.78f, 0.85f, 1f);
            var le = hint.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 72f;
        }

        static Text CreateText(RectTransform parent, string content, int size, FontStyle style, TextAnchor align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null)
                t.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            t.text = content;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = Color.white;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 24f;
            le.flexibleWidth = 1f;
            return t;
        }

        static Toggle CreateToggle(RectTransform parent, string label)
        {
            var row = new GameObject("ToggleRow", typeof(RectTransform));
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.SetParent(parent, false);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlHeight = true;
            h.childForceExpandHeight = false;
            h.childControlWidth = false;
            h.childForceExpandWidth = false;

            var toggleGo = new GameObject("Toggle", typeof(RectTransform));
            toggleGo.transform.SetParent(rowRt, false);
            var toggle = toggleGo.AddComponent<Toggle>();
            var bg = toggleGo.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.25f, 0.28f, 1f);
            var toggleRt = toggleGo.GetComponent<RectTransform>();
            toggleRt.sizeDelta = new Vector2(28f, 28f);
            var leT = toggleGo.AddComponent<LayoutElement>();
            leT.preferredWidth = 28f;
            leT.preferredHeight = 28f;

            var checkGo = new GameObject("Check", typeof(RectTransform));
            checkGo.transform.SetParent(toggleGo.transform, false);
            var checkRt = checkGo.GetComponent<RectTransform>();
            checkRt.anchorMin = Vector2.zero;
            checkRt.anchorMax = Vector2.one;
            checkRt.offsetMin = new Vector2(4f, 4f);
            checkRt.offsetMax = new Vector2(-4f, -4f);
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.color = new Color(0.35f, 0.85f, 0.45f, 1f);

            toggle.graphic = checkImg;
            toggle.targetGraphic = bg;

            var labelText = CreateText(rowRt, label, 16, FontStyle.Normal, TextAnchor.MiddleLeft);
            var leL = labelText.gameObject.AddComponent<LayoutElement>();
            leL.flexibleWidth = 1f;
            leL.minHeight = 32f;

            return toggle;
        }

        static InputField CreateLabeledField(RectTransform parent, string title, string placeholderHint)
        {
            var block = new GameObject("FieldBlock", typeof(RectTransform));
            var blockRt = block.GetComponent<RectTransform>();
            blockRt.SetParent(parent, false);
            var v = block.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlHeight = true;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;

            var tTitle = CreateText(blockRt, title, 15, FontStyle.Bold, TextAnchor.UpperLeft);
            var leTitle = tTitle.gameObject.AddComponent<LayoutElement>();
            leTitle.minHeight = 22f;

            var hint = CreateText(blockRt, placeholderHint, 13, FontStyle.Italic, TextAnchor.UpperLeft);
            hint.color = new Color(0.65f, 0.7f, 0.78f, 1f);
            var leHint = hint.gameObject.AddComponent<LayoutElement>();
            leHint.minHeight = 18f;

            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(blockRt, false);
            var inputRt = inputGo.GetComponent<RectTransform>();
            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.18f, 0.19f, 0.22f, 1f);
            var field = inputGo.AddComponent<InputField>();
            field.lineType = InputField.LineType.SingleLine;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(inputGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 6f);
            textRt.offsetMax = new Vector2(-8f, -6f);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            text.fontSize = 16;
            text.color = Color.white;
            text.supportRichText = false;
            field.textComponent = text;

            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(inputGo.transform, false);
            var phRt = phGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(8f, 6f);
            phRt.offsetMax = new Vector2(-8f, -6f);
            var ph = phGo.AddComponent<Text>();
            ph.font = text.font;
            ph.fontSize = 16;
            ph.fontStyle = FontStyle.Italic;
            ph.color = new Color(1f, 1f, 1f, 0.35f);
            ph.text = "输入数字，如 0 / 0.1 / 50";
            ph.raycastTarget = false;
            field.placeholder = ph;

            var leIn = inputGo.AddComponent<LayoutElement>();
            leIn.minHeight = 36f;
            leIn.flexibleWidth = 1f;

            return field;
        }

        void HookField(InputField field, System.Action<float> apply)
        {
            if (field == null) return;
            field.contentType = InputField.ContentType.DecimalNumber;
            field.onEndEdit.AddListener(s =>
            {
                if (TryParseFloat(s, out var v))
                    apply(v);
            });
        }

        static bool TryParseFloat(string s, out float v)
        {
            v = 0f;
            if (string.IsNullOrWhiteSpace(s))
                return true;
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ||
                   float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out v);
        }

        void OnOverrideToggled(bool on)
        {
            EquipmentStatDebugState.OverrideEnabled = on;
        }

        void SyncFieldsFromState()
        {
            if (_overrideToggle != null)
                _overrideToggle.isOn = EquipmentStatDebugState.OverrideEnabled;
            SetFieldText(_wingMul, EquipmentStatDebugState.WingClimbSpeedMul);
            SetFieldText(_charCoinsMul, EquipmentStatDebugState.CharacterCoinsMul);
            SetFieldText(_charClimbMul, EquipmentStatDebugState.CharacterClimbSpeedMul);
            SetFieldText(_trailCoinsMul, EquipmentStatDebugState.TrailCoinsMul);
            SetFieldText(_pet1Add, EquipmentStatDebugState.PetSlot1CoinsAdd);
            SetFieldText(_pet2Add, EquipmentStatDebugState.PetSlot2CoinsAdd);
            SetFieldText(_pet3Add, EquipmentStatDebugState.PetSlot3CoinsAdd);
        }

        static void SetFieldText(InputField f, float value)
        {
            if (f != null)
                f.text = value == 0f ? "" : value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
