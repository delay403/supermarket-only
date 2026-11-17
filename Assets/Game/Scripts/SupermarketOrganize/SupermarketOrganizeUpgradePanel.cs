using System;
using System.Collections.Generic;
using Game.Audio;
using Game.Economy;
using Game.Save;
using Game.SupermarketOrganize.Config;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>难度选择页上的局外商店弹窗，复用原升级面板。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeUpgradePanel : MonoBehaviour, IUiPanelController
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform mainPanel;
        [SerializeField] private Button maskCloseButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private SupermarketOrganizeUpgradeUiConfig uiConfig;
        [SerializeField] private RectTransform skillGridContent;
        [SerializeField] private RectTransform attributeGridContent;
        [SerializeField] private SupermarketOrganizeUpgradeRowUi itemPrefab;
        [SerializeField] private SupermarketOrganizeUpgradeDetailUi detailUi;
        [SerializeField] private Text titleText;
        [SerializeField] private Text coinText;
        [SerializeField] private Button skillTabButton;
        [SerializeField] private Button attributeTabButton;
        [SerializeField] private GameObject skillPage;
        [SerializeField] private GameObject attributePage;
        [SerializeField] private Text skillFooterText;
        [SerializeField] private Text attributeFooterText;

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.38f;
        [SerializeField] private float settleDuration = 0.28f;
        [SerializeField] private float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        readonly List<SupermarketOrganizeUpgradeRowUi> _cells = new List<SupermarketOrganizeUpgradeRowUi>();
        readonly List<SupermarketOrganizeUpgradeEntryDef> _entries = new List<SupermarketOrganizeUpgradeEntryDef>();
        bool _shopChromeReady;
        bool _showingSkills = true;
        Font _font;
        Image _skillTabImage;
        Image _attributeTabImage;

        public static event Action Closed;

        public UiPanelId PanelId => UiPanelId.SupermarketOrganizeUpgrade;

        void Awake()
        {
            maskCloseButton?.onClick.AddListener(OnDismissClicked);
            closeButton?.onClick.AddListener(OnDismissClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable()
        {
            UiCoordinator.Register(this);
            SupermarketOrganizeProgressionService.SkillUpgraded += OnProgressionChanged;
            SupermarketOrganizeProgressionService.AttributeUpgraded += OnProgressionChanged;
            EconomyDisplayEvents.Changed += RefreshAll;
        }

        void OnDisable()
        {
            UiCoordinator.Unregister(this);
            SupermarketOrganizeProgressionService.SkillUpgraded -= OnProgressionChanged;
            SupermarketOrganizeProgressionService.AttributeUpgraded -= OnProgressionChanged;
            EconomyDisplayEvents.Changed -= RefreshAll;
        }

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null)
        {
            if (!SupermarketOrganizeHomePanel.CanOpenOutOfRunShop())
                return;

            EnsureShopChrome();
            EnsureBuilt();
            ShowSkillPage();
            RefreshAll();
            Show();
        }

        public void Close() => Hide();

        void OnProgressionChanged(SupermarketSkillId _, int __) => RefreshAll();

        void OnProgressionChanged(SupermarketAttributeId _, int __) => RefreshAll();

        void EnsureBuilt()
        {
            uiConfig ??= SupermarketOrganizeUpgradeUiConfig.ResolveRuntime();
            _entries.Clear();
            _entries.AddRange(uiConfig.GetSortedEntries());

            if (_cells.Count > 0)
                return;

            var font = ResolveFont();
            var buySprite = uiConfig != null ? uiConfig.shopBuyButtonSprite : null;
            var coinIcon = uiConfig != null ? uiConfig.coinIconSprite : null;

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var parent = entry.kind == SupermarketOrganizeUpgradeEntryKind.Skill
                    ? skillGridContent
                    : attributeGridContent;
                if (parent == null)
                    continue;

                var cell = SupermarketOrganizeUpgradeRowUi.CreateShopCard(parent, font, buySprite, coinIcon);
                cell.name = $"ShopCard_{i}";
                cell.Bind(entry, uiConfig.ResolveShopCardBackground(entry));
                _cells.Add(cell);
            }
        }

        public void RefreshAll()
        {
            uiConfig ??= SupermarketOrganizeUpgradeUiConfig.ResolveRuntime();
            RefreshCoinDisplay();
            for (var i = 0; i < _cells.Count; i++)
            {
                if (i < _entries.Count)
                    _cells[i]?.Bind(_entries[i], uiConfig.ResolveShopCardBackground(_entries[i]));
                else
                    _cells[i]?.Refresh();
            }
        }

        void RefreshCoinDisplay()
        {
            if (coinText != null)
                coinText.text = EconomyDisplayFormat.Format(ModeSaveAccess.Coins);
        }

        void EnsureShopChrome()
        {
            if (_shopChromeReady)
                return;

            uiConfig ??= SupermarketOrganizeUpgradeUiConfig.ResolveRuntime();
            _font = ResolveFont();
            HideLegacyLayout();
            ApplyModalLayout();
            EnsureTitle();
            EnsureCoinHud();
            EnsureTabsAndPages();
            _shopChromeReady = true;
        }

        void HideLegacyLayout()
        {
            if (mainPanel == null)
                return;

            SetChildActive(mainPanel, "LeftColumn", false);
            SetChildActive(mainPanel, "RightColumn", false);
        }

        void ApplyModalLayout()
        {
            if (mainPanel == null)
                return;

            mainPanel.anchorMin = new Vector2(0.5f, 0.5f);
            mainPanel.anchorMax = new Vector2(0.5f, 0.5f);
            mainPanel.pivot = new Vector2(0.5f, 0.5f);
            mainPanel.anchoredPosition = Vector2.zero;
            mainPanel.sizeDelta = new Vector2(1120f, 640f);
        }

        void EnsureTitle()
        {
            if (titleText == null)
                titleText = FindChildComponent<Text>(mainPanel, "Title");

            if (titleText != null)
            {
                titleText.text = "道具商店";
                if (_font != null)
                    titleText.font = _font;
            }
        }

        void EnsureCoinHud()
        {
            if (coinText != null)
                return;

            var hud = CreateRect("ShopCoinHud", mainPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(168f, 38f));
            hud.pivot = new Vector2(0f, 1f);
            var frame = hud.gameObject.AddComponent<Image>();
            if (uiConfig != null && uiConfig.coinFrameSprite != null)
            {
                frame.sprite = uiConfig.coinFrameSprite;
                frame.preserveAspect = true;
            }
            else
            {
                frame.color = new Color(0f, 0f, 0f, 0.45f);
            }

            frame.raycastTarget = false;

            var icon = CreateRect("CoinIcon", hud, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(28f, 28f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            if (uiConfig != null)
                iconImage.sprite = uiConfig.coinIconSprite;

            var textRt = CreateRect("CoinText", hud, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-16f, 0f));
            textRt.offsetMin = new Vector2(36f, 0f);
            textRt.offsetMax = new Vector2(-8f, 0f);
            coinText = textRt.gameObject.AddComponent<Text>();
            StyleText(coinText, 22, TextAnchor.MiddleCenter);
        }

        void EnsureTabsAndPages()
        {
            if (skillTabButton == null || attributeTabButton == null)
            {
                var tabBar = CreateRect("TabBar", mainPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(420f, 48f));
                skillTabButton = CreateTabButton(tabBar, "SkillTab", "主动道具", new Vector2(-110f, 0f));
                attributeTabButton = CreateTabButton(tabBar, "AttributeTab", "永久强化", new Vector2(110f, 0f));
                _skillTabImage = skillTabButton.GetComponent<Image>();
                _attributeTabImage = attributeTabButton.GetComponent<Image>();
                skillTabButton.onClick.AddListener(OnSkillTabClicked);
                attributeTabButton.onClick.AddListener(OnAttributeTabClicked);
            }

            if (skillPage == null || skillGridContent == null)
            {
                skillPage = CreatePage("SkillPage", "购买后永久解锁，升级可增强效果并缩短冷却", out skillGridContent, out skillFooterText);
            }

            if (attributePage == null || attributeGridContent == null)
            {
                attributePage = CreatePage("AttributePage", "购买后永久保存，下一局开始时生效", out attributeGridContent, out attributeFooterText);
            }
        }

        Button CreateTabButton(RectTransform parent, string name, string label, Vector2 pos)
        {
            var rt = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(190f, 48f));
            var image = rt.gameObject.AddComponent<Image>();
            if (uiConfig != null && uiConfig.shopTabButtonSprite != null)
            {
                image.sprite = uiConfig.shopTabButtonSprite;
                image.type = Image.Type.Sliced;
            }
            else
            {
                image.color = new Color(0.85f, 0.6f, 0.2f, 1f);
            }

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateRect("Label", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Text>();
            StyleText(text, 24, TextAnchor.MiddleCenter);
            text.text = label;
            return button;
        }

        GameObject CreatePage(string name, string footer, out RectTransform grid, out Text footerText)
        {
            var page = CreateRect(name, mainPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            page.offsetMin = new Vector2(24f, 16f);
            page.offsetMax = new Vector2(-24f, -140f);

            var footerRt = CreateRect("Footer", page, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(900f, 32f));
            footerRt.pivot = new Vector2(0.5f, 0f);
            footerText = footerRt.gameObject.AddComponent<Text>();
            StyleText(footerText, 16, TextAnchor.MiddleCenter);
            footerText.color = new Color(1f, 1f, 1f, 0.9f);
            footerText.text = footer;

            grid = CreateRect("Grid", page, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            grid.offsetMin = new Vector2(0f, 40f);
            grid.offsetMax = Vector2.zero;
            var layout = grid.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return page.gameObject;
        }

        void OnSkillTabClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            ShowSkillPage();
        }

        void OnAttributeTabClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            ShowAttributePage();
        }

        void ShowSkillPage()
        {
            _showingSkills = true;
            if (skillPage != null)
                skillPage.SetActive(true);
            if (attributePage != null)
                attributePage.SetActive(false);
            RefreshTabVisuals();
        }

        void ShowAttributePage()
        {
            _showingSkills = false;
            if (skillPage != null)
                skillPage.SetActive(false);
            if (attributePage != null)
                attributePage.SetActive(true);
            RefreshTabVisuals();
        }

        void RefreshTabVisuals()
        {
            if (_skillTabImage != null)
                _skillTabImage.color = _showingSkills ? Color.white : new Color(1f, 1f, 1f, 0.62f);
            if (_attributeTabImage != null)
                _attributeTabImage.color = !_showingSkills ? Color.white : new Color(1f, 1f, 1f, 0.62f);
        }

        void Show()
        {
            EnsureSlideHost();
            transform.SetAsLastSibling();
            slideHost.PlayOpen();
        }

        void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;

            slideHost.RequestClose(() => Closed?.Invoke());
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
        }

        void EnsureSlideHost()
        {
            UiSlidingPanelHost.EnsureOn(
                this,
                ref slideHost,
                root,
                mainPanel,
                UiSlidingPanelHost.SlideDirection.FromBottom,
                slideDuration,
                settleDuration,
                overshootDownPx);
        }

        void OnDismissClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            Hide();
        }

        Font ResolveFont()
        {
            if (_font != null)
                return _font;

            var loaded = Resources.Load<Font>("喜脉体");
            if (loaded != null)
                return loaded;

            if (titleText == null)
                titleText = FindChildComponent<Text>(mainPanel, "Title");
            return titleText != null ? titleText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        void StyleText(Text text, int fontSize, TextAnchor alignment)
        {
            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        static void SetChildActive(Transform parent, string childName, bool active)
        {
            if (parent == null)
                return;

            var child = parent.Find(childName);
            if (child != null)
                child.gameObject.SetActive(active);
        }

        static T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
            if (parent == null)
                return null;

            var child = parent.Find(childName);
            if (child == null)
            {
                var nested = parent.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < nested.Length; i++)
                {
                    if (nested[i] != null && nested[i].name == childName)
                    {
                        child = nested[i];
                        break;
                    }
                }
            }

            return child != null ? child.GetComponent<T>() : null;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }

            return rt;
        }
    }
}
