using Game.Core;
using Game.UI.MainHome;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.FairyGUI
{
    /// <summary>
    /// MainHome UGUI 流程：先显示主页 Lobby（开始按钮），点击后再显示已预初始化的选关页。
    /// 单模式包体仅作入口：不显示选关页，开始按钮直接进入当前 GameProduct 启用的玩法。
    /// </summary>
    public sealed class MainHomeLobbyController : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private MainHomeGameModeSelectPanel modeSelectPanel;
        [SerializeField] private GameObject homeLobbyRoot;
        [SerializeField] private GameObject modeSelectRoot;
        [SerializeField] private Button homeStartButton;
        [SerializeField] private bool hideModeSelectOnAwake = true;

        [Header("Optional auto-create")]
        [SerializeField] private bool createHomeStartButtonIfMissing = true;
        [SerializeField] private string homeStartButtonObjectName = "BtnStart";

        static readonly string[] HomeStartButtonNames = { "BtnStart", "BtnHomeStart" };

        bool _homeStartBound;
        bool _singleModeHomeEntry;

        void Awake()
        {
            EnsureUiInputStack();
            ResolveReferences();
            ApplyProductScopedHomeLayout();
            TryBindHomeStartButton();
        }

        void OnEnable() => TryBindHomeStartButton();

        void OnDisable() => UnbindHomeStartButton();

        void ApplyProductScopedHomeLayout()
        {
            var availableCount = ResolveAvailableModeCount();
            _singleModeHomeEntry = availableCount <= 1;

            if (MainHomeNavigationContext.ConsumeOpenToModeSelect())
            {
                if (homeLobbyRoot != null)
                    homeLobbyRoot.SetActive(false);

                SetModeSelectVisible(true);
                modeSelectPanel?.PrepareWhileHidden();
                modeSelectPanel?.ShowPrepared();
                return;
            }

            if (_singleModeHomeEntry)
            {
                if (homeLobbyRoot != null)
                    homeLobbyRoot.SetActive(true);

                SetModeSelectVisible(false);
                ClearStaticModeSelectPlaceholderArt();
                return;
            }

            modeSelectPanel?.PrepareWhileHidden();

            if (hideModeSelectOnAwake)
                SetModeSelectVisible(false);
            else
                SetModeSelectVisible(true);
        }

        int ResolveAvailableModeCount()
        {
            if (modeSelectPanel != null)
                return modeSelectPanel.GetAvailableModeCount();

            var settings = GameProductSettings.ResolveActive();
            return settings != null ? settings.BuildAvailableModes().Count : 0;
        }

        void ClearStaticModeSelectPlaceholderArt()
        {
            if (modeSelectRoot == null)
                return;

            var nodes = modeSelectRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || node.name != "PrewImage")
                    continue;

                if (node.TryGetComponent<Image>(out var image))
                    image.sprite = null;
            }
        }

        void ResolveReferences()
        {
            if (modeSelectPanel == null)
                modeSelectPanel = GetComponent<MainHomeGameModeSelectPanel>();

            if (modeSelectPanel == null)
                modeSelectPanel = FindObjectOfType<MainHomeGameModeSelectPanel>(true);

            if (modeSelectRoot == null && modeSelectPanel != null)
                modeSelectRoot = modeSelectPanel.gameObject;

            if (homeLobbyRoot == null && modeSelectPanel != null)
            {
                var canvas = modeSelectPanel.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    var canvasRt = canvas.transform;
                    if (canvasRt.childCount > 0)
                        homeLobbyRoot = canvasRt.GetChild(0).gameObject;
                }
            }

            if (homeStartButton == null && homeLobbyRoot != null)
                homeStartButton = FindHomeStartButton(homeLobbyRoot);

            if (homeStartButton == null && createHomeStartButtonIfMissing)
                homeStartButton = EnsureHomeStartButton();
        }

        Button FindHomeStartButton(GameObject root)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (var n = 0; n < HomeStartButtonNames.Length; n++)
            {
                var targetName = HomeStartButtonNames[n];
                for (var i = 0; i < buttons.Length; i++)
                {
                    var btn = buttons[i];
                    if (btn != null && btn.name == targetName)
                        return btn;
                }
            }

            if (!string.IsNullOrEmpty(homeStartButtonObjectName))
            {
                for (var i = 0; i < buttons.Length; i++)
                {
                    var btn = buttons[i];
                    if (btn != null && btn.name == homeStartButtonObjectName)
                        return btn;
                }
            }

            return null;
        }

        Button EnsureHomeStartButton()
        {
            if (homeLobbyRoot == null)
                return null;

            var existingBtn = FindHomeStartButton(homeLobbyRoot);
            if (existingBtn != null)
                return existingBtn;

            var existing = homeLobbyRoot.transform.Find(homeStartButtonObjectName);
            if (existing != null && existing.TryGetComponent<Button>(out existingBtn))
                return existingBtn;

            return CreateFallbackHomeStartButton();
        }

        Button CreateFallbackHomeStartButton()
        {
            var go = new GameObject(homeStartButtonObjectName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(homeLobbyRoot.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 96f);
            rt.sizeDelta = new Vector2(264f, 102f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.2f, 0.75f, 0.35f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 32;
            text.text = "开始游戏";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return go.GetComponent<Button>();
        }

        void EnsureUiInputStack()
        {
            EnsureCanvasScaleValid();
            EnsureGraphicRaycasters();
            EnsureEventSystem();
            NormalizeOverlayCanvases();
        }

        void EnsureCanvasScaleValid()
        {
            var canvases = FindObjectsOfType<Canvas>(true);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null)
                    continue;

                var rt = canvas.transform as RectTransform;
                if (rt == null)
                    continue;

                if (rt.localScale.sqrMagnitude < 0.01f)
                    rt.localScale = Vector3.one;
            }
        }

        void EnsureGraphicRaycasters()
        {
            var canvases = FindObjectsOfType<Canvas>(true);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || !canvas.enabled || !canvas.gameObject.activeInHierarchy)
                    continue;

                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        void EnsureEventSystem()
        {
            var systems = FindObjectsOfType<EventSystem>(true);
            EventSystem primary = null;

            for (var i = 0; i < systems.Length; i++)
            {
                var es = systems[i];
                if (es == null)
                    continue;

                if (!es.gameObject.activeSelf)
                    es.gameObject.SetActive(true);

                if (!es.enabled)
                    es.enabled = true;

                if (primary == null)
                    primary = es;
                else
                    es.gameObject.SetActive(false);
            }

            if (primary != null)
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        void NormalizeOverlayCanvases()
        {
            var canvases = FindObjectsOfType<Canvas>(true);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null)
                    continue;

                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    canvas.worldCamera = null;
            }
        }

        void TryBindHomeStartButton()
        {
            if (_homeStartBound || homeStartButton == null)
                return;

            homeStartButton.onClick.AddListener(OnHomeStartClicked);
            _homeStartBound = true;
        }

        void UnbindHomeStartButton()
        {
            if (!_homeStartBound || homeStartButton == null)
                return;

            homeStartButton.onClick.RemoveListener(OnHomeStartClicked);
            _homeStartBound = false;
        }

        void OnHomeStartClicked()
        {
            if (modeSelectPanel != null && modeSelectPanel.TryLaunchIfSingleMode())
                return;

            if (_singleModeHomeEntry || ResolveAvailableModeCount() <= 1)
            {
                Debug.LogWarning("[MainHomeLobbyController] 当前 GameProduct 仅配置了一个模式，但无法进入玩法。请检查 enabled 与 Build Settings 场景。");
                return;
            }

            modeSelectPanel?.PrepareWhileHidden();

            if (homeLobbyRoot != null)
                homeLobbyRoot.SetActive(false);

            SetModeSelectVisible(true);
            modeSelectPanel?.ShowPrepared();
        }

        void SetModeSelectVisible(bool visible)
        {
            if (modeSelectRoot != null)
            {
                modeSelectRoot.SetActive(visible);
                return;
            }

            modeSelectPanel?.SetPanelVisible(visible);
        }
    }
}
