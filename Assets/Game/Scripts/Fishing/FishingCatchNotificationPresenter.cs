using System;
using System.Collections;
using Game.Audio;
using Game.Core;
using Game.Fishing;
using Game.Hud;
using Game.UI.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Fishing
{
    /// <summary>
    /// 钓获成功后在 HUD 弹出鱼信息 Toast；与 HandFishRoot 展示动画同时触发（镜头、庆祝特效同步）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingCatchNotificationPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerFishingController fishing;
        [SerializeField] private PlayerFishingCatchShowcaseDriver catchShowcase;
        [SerializeField] private FishingCatchRewardToastView toastPrefab;
        [SerializeField] private RectTransform toastAnchor;
        [SerializeField] private Vector2 toastAnchoredPosition = new Vector2(0f, -60f);
        [SerializeField] private float toastAnchorY = 0.62f;
        [SerializeField] private FishingCameraFeedback cameraFeedback;
        [SerializeField] private FishingPresentationFx presentationFx;

        Coroutine _pendingToastRoutine;
        FishingService.CatchResult? _pendingCatchResult;
        bool _toastShownForCurrentCatch;

        /// <summary>钓获奖励 Toast 已显示（与展示动画同时）。</summary>
        public event Action CatchRewardToastDisplayed;

        /// <summary>钓获成功、Toast 尚未弹出（等待展示动画开始）。</summary>
        public bool IsAwaitingCatchRewardToast =>
            _pendingCatchResult.HasValue && !_toastShownForCurrentCatch;

        void Awake()
        {
            if (fishing == null)
                fishing = GetComponent<PlayerFishingController>();
            if (catchShowcase == null && fishing != null)
                catchShowcase = fishing.GetComponent<PlayerFishingCatchShowcaseDriver>();
            if (presentationFx == null && fishing != null)
                presentationFx = fishing.GetComponent<FishingPresentationFx>();
            if (cameraFeedback == null && fishing != null)
            {
                cameraFeedback = fishing.GetComponent<FishingCameraFeedback>();
                if (cameraFeedback == null)
                    cameraFeedback = fishing.gameObject.AddComponent<FishingCameraFeedback>();
            }
        }

        void OnEnable()
        {
            if (fishing != null)
                fishing.CatchCompleted += OnCatchCompleted;

            if (catchShowcase == null && fishing != null)
                catchShowcase = fishing.GetComponent<PlayerFishingCatchShowcaseDriver>();

            if (catchShowcase != null)
                catchShowcase.CatchShowcaseStarted += OnCatchShowcaseStarted;
        }

        void OnDisable()
        {
            if (fishing != null)
                fishing.CatchCompleted -= OnCatchCompleted;

            if (catchShowcase != null)
                catchShowcase.CatchShowcaseStarted -= OnCatchShowcaseStarted;

            CancelPendingToast();
            _pendingCatchResult = null;
            _toastShownForCurrentCatch = false;
        }

        void OnCatchCompleted(FishingService.CatchResult result)
        {
            if (!result.success || result.fish == null)
                return;

            CancelPendingToast();
            _pendingCatchResult = result;
            _toastShownForCurrentCatch = false;
            _pendingToastRoutine = StartCoroutine(CoWaitForShowcaseStartOrPresentationEnd());
        }

        void OnCatchShowcaseStarted() => TryShowPendingToast();

        IEnumerator CoWaitForShowcaseStartOrPresentationEnd()
        {
            var lineDriver = fishing != null ? fishing.GetComponent<PlayerFishingLineDriver>() : null;

            while (!_toastShownForCurrentCatch
                   && lineDriver != null
                   && lineDriver.IsPostCatchPresentationActive)
            {
                yield return null;
            }

            TryShowPendingToast();
            _pendingToastRoutine = null;
        }

        void TryShowPendingToast()
        {
            if (_toastShownForCurrentCatch || !_pendingCatchResult.HasValue)
                return;

            var result = _pendingCatchResult.Value;
            _pendingCatchResult = null;
            _toastShownForCurrentCatch = true;
            ShowToast(result);
        }

        void CancelPendingToast()
        {
            if (_pendingToastRoutine == null)
                return;

            StopCoroutine(_pendingToastRoutine);
            _pendingToastRoutine = null;
        }

        void ShowToast(FishingService.CatchResult result)
        {
            CatchRewardToastDisplayed?.Invoke();
            cameraFeedback?.PlayCatchToastShake();

            var settleSfx = presentationFx != null
                ? presentationFx.ResolveCelebrateSfx()
                : SfxId.FishingCatchSettle;
            var settlePos = presentationFx != null
                ? presentationFx.FootWorldPosition
                : fishing != null ? fishing.transform.position : (Vector3?)null;

            if (toastPrefab != null)
            {
                var anchor = ResolveAnchor();
                if (anchor == null)
                {
                    Debug.LogWarning("[FishingCatchNotificationPresenter] 未找到 HUD Canvas，无法显示钓获 Toast。", this);
                    return;
                }

                var inst = Instantiate(toastPrefab, anchor);
                PrepareToast(inst.transform as RectTransform, anchor);
                inst.Show(result, settleSfx, settlePos);
                return;
            }

            SpawnRuntimeToast(result, settleSfx, settlePos);
        }

        void SpawnRuntimeToast(
            FishingService.CatchResult result,
            SfxId settleSfx,
            Vector3? settlePos)
        {
            var anchor = ResolveAnchor();
            if (anchor == null)
            {
                Debug.LogWarning("[FishingCatchNotificationPresenter] 未找到 HUD Canvas，无法生成运行时 Toast。", this);
                return;
            }

            var root = new GameObject("FishingCatchToast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var rt = root.GetComponent<RectTransform>();
            PrepareToast(rt, anchor);
            root.transform.SetParent(anchor, false);

            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.18f, 0.88f);
            rt.sizeDelta = new Vector2(320f, 120f);

            var iconGo = CreateChild<Image>(root.transform, "FishIcon", new Vector2(-110f, 0f), new Vector2(72f, 72f));
            iconGo.sprite = result.fish.icon;
            iconGo.preserveAspect = true;
            iconGo.enabled = result.fish.icon != null;

            CreateChild<Text>(root.transform, "FishName", new Vector2(24f, 22f), new Vector2(220f, 36f),
                string.IsNullOrEmpty(result.fish.displayName) ? result.fish.fishId : result.fish.displayName, 22);

            CreateChild<Text>(root.transform, "CoinValue", new Vector2(24f, -18f), new Vector2(220f, 32f),
                Game.Economy.EconomyDisplayFormat.Format(result.coinsAwarded < 0 ? 0 : result.coinsAwarded), 26);

            if (result.wasFirstDiscovery)
            {
                var badge = CreateChild<Text>(root.transform, "UndiscoveredBadge", new Vector2(120f, 22f),
                    new Vector2(120f, 28f), "未解锁", 18);
                badge.color = new Color(1f, 0.85f, 0.35f);
            }

            var toast = root.AddComponent<FishingCatchRewardToastView>();
            toast.Show(result, settleSfx, settlePos);
        }

        void PrepareToast(RectTransform toast, RectTransform anchor)
        {
            if (toast == null)
                return;

            toast.SetParent(anchor, false);
            toast.anchorMin = new Vector2(0.5f, toastAnchorY);
            toast.anchorMax = new Vector2(0.5f, toastAnchorY);
            toast.pivot = new Vector2(0.5f, 0.5f);
            toast.anchoredPosition = toastAnchoredPosition;
            toast.gameObject.SetActive(true);
            toast.SetAsLastSibling();
        }

        static T CreateChild<T>(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string text = null, int fontSize = 20)
            where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            if (typeof(T) == typeof(Text))
            {
                var label = go.AddComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.alignment = TextAnchor.MiddleLeft;
                label.color = Color.white;
                label.fontSize = fontSize;
                label.text = text ?? string.Empty;
                return label as T;
            }

            return go.AddComponent<T>();
        }

        RectTransform ResolveAnchor()
        {
            if (toastAnchor != null)
                return toastAnchor;

            var hud = UnityObjectFinder.FindFirst<GameHudUguiPanel>();
            if (hud != null && hud.CatchToastAnchor != null)
                return hud.CatchToastAnchor;

            return ResolveBestHudCanvas();
        }

        static RectTransform ResolveBestHudCanvas()
        {
            Canvas best = null;
#if UNITY_2023_1_OR_NEWER
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
#endif
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || !canvas.isActiveAndEnabled)
                    continue;

                if (canvas.renderMode == RenderMode.WorldSpace)
                    continue;

                if (best == null || canvas.sortingOrder >= best.sortingOrder)
                    best = canvas;
            }

            return best != null ? best.transform as RectTransform : null;
        }
    }
}
