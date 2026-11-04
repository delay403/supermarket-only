using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Collectibles;
using Game.Collectibles.Pets;
using Game.Core.Config;
using Game.Economy;
using Game.Save;
using Game.UI.Framework;
using Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>宠物孵化（抽卡）：单抽/五连、概率 ScrollView、结算子页（复用 PetFusionBatchRowView）。</summary>
    public sealed class PetHatchPanel : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.PetHatch;

        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Button closeButton;
        public Button probButton;
        public GameObject probOverlay;

        [Tooltip("概率弹窗的可缩放内容根（蓝底面板）。")]
        public RectTransform probPopAnimRoot;

        [Tooltip("概率列表 ScrollRect；Content 上挂 GridLayoutGroup。")]
        public ScrollRect probScroll;

        public RectTransform probContent;
        public Button probCloseButton;
        public Button hatchOneButton;
        public Text hatchOneCostText;
        public Button hatchFiveButton;
        public Text hatchFiveCostText;

        public GameObject resultOverlay;

        [Tooltip("结算弹窗中心面板（用于炸开缩放入场）。")]
        public RectTransform resultPopAnimRoot;

        public Text resultTitleText;
        public RectTransform resultGrid;
        public Button resultToBackpackButton;
        public Button resultHatchAgainButton;
        public Text resultHatchAgainLabel;

        [Tooltip("点击孵化结算遮罩（半透明底）关闭结算层，不关闭整个孵化界面。")]
        public Button resultOverlayMaskCloseButton;

        [SerializeField] private QualityVisualLibrary qualityVisualLibrary;

        [Tooltip("孵化结果格子预制体（须含 PetFusionBatchRowView），必选。")]
        [SerializeField] private PetFusionBatchRowView hatchResultRowPrefab;

        [Tooltip("概率列表行预制体（须含 PetFusionBatchRowView），必选。")]
        [SerializeField] private PetFusionBatchRowView probRowPrefab;

        [Header("Hatch pools (optional catalog)")]
        [Tooltip("不为空且内含有效卡池时：左右切换卡池、消耗与概率按配置；否则沿用全局 PetDef.hatchWeight 逻辑。")]
        [SerializeField] private PetHatchPoolsCatalog hatchPoolsCatalog;

        [Tooltip("当前卡池名称（如「普通孵蛋」）。未配置目录时可留空。")]
        public Text poolDisplayNameText;

        public Button poolPrevButton;
        public Button poolNextButton;

        [Tooltip("切换卡池时左右滑动的内容根（建议为标题+中央插画+按钮的父节点）；未赋值则切换时无位移动画。")]
        public RectTransform poolSlideRoot;

        [Tooltip("单抽按钮上方的货币图标。")]
        public Image hatchOneCurrencyIcon;

        [Tooltip("五连按钮上方的货币图标。")]
        public Image hatchFiveCurrencyIcon;

        [Tooltip("卡池 currencyIcon 为空时的金币图标。")]
        [SerializeField] private Sprite fallbackCoinCurrencySprite;

        [Tooltip("卡池 currencyIcon 为空时的钻石图标。")]
        [SerializeField] private Sprite fallbackGemCurrencySprite;

        [Tooltip("切换卡池时横向滑出距离（anchored 像素）。")]
        public float poolSwipeDistancePx = 420f;

        [Tooltip("滑出 / 滑入各阶段时长（秒）。")]
        public float poolSwipeHalfDuration = 0.18f;

        [Header("Egg · 卡池外观")]
        [Tooltip("中央蛋 Image（与碎裂图层同一父节点时，Idle 动画动的是 eggIdleAnimRoot）。")]
        public Image eggIconImage;

        [Tooltip("待机动画与破壳过程摇动的 RectTransform（一般为蛋根节点）。")]
        public RectTransform eggIdleAnimRoot;

        [Tooltip("三个碎裂叠加层，破壳时按顺序渐显。")]
        public Image eggCrackFrag1;
        public Image eggCrackFrag2;
        public Image eggCrackFrag3;

        [Tooltip("无多卡池或未配置 eggIcon 时使用的蛋图。")]
        [SerializeField] private Sprite defaultEggSprite;

        [Tooltip("未使用卡池目录时的卡池标题文案。")]
        [SerializeField] private string legacyPoolDisplayName = "宠物孵化";

        [Header("Egg · 池切换红点（可选）")]
        [Tooltip("上一池可单抽（货币足够且背包有位置）时显示。")]
        [SerializeField] private GameObject poolPrevRedDot;

        [Tooltip("下一池可单抽时显示。")]
        [SerializeField] private GameObject poolNextRedDot;

        [Header("Egg · 待机动画（多段摇晃+弹跳）")]
        [SerializeField] private float eggIdleTiltDegrees = 7f;

        [SerializeField] private float eggIdleHopAnchoredPx = 8f;

        [SerializeField] private float eggIdleSwayHalfSec = 0.32f;

        [SerializeField] private float eggIdleFastJitterHalfSec = 0.2f;

        [SerializeField] private float eggIdlePauseBetweenCyclesSec = 0.35f;

        [Header("Egg · 抽卡过程动画")]
        [SerializeField] private bool hatchIntroFxEnabled = true;

        [SerializeField] private int preCrackSharpShakeCount = 3;

        [SerializeField] private float preCrackSharpShakeStepSec = 0.065f;

        [SerializeField] private float preCrackSharpTiltDeg = 16f;

        [SerializeField] private float crackLayerFadeSec = 0.5f;

        [SerializeField] private float eggShakeAfterCrackPunchDuration = 0.26f;

        [SerializeField] private float eggShakeAfterCrackPunchStrengthZ = 22f;

        [SerializeField] private int eggShakeAfterCrackPunchVibrato = 14;

        [Header("Animations")]
        public bool resultIntroPopEnabled = true;
        public float resultIntroInDuration = 0.22f;
        public float resultIntroSettleDuration = 0.28f;
        public float resultIntroPeakScale = 1.1f;
        public float resultIntroStartScale = 0.08f;
        [Tooltip("多抽时每个格子从中心滑到排布位的间隔（秒）。")]
        public float resultCellStaggerSec = 0.07f;
        [Tooltip("单格位移动画时长（秒）。")]
        public float resultCellSlideDuration = 0.38f;

        [Header("Result title (after slot deal)")]
        [Tooltip("孵化成功标题：在格子飞入结束后再弹出，与整个结算面板的入场可分开控制。")]
        public bool resultTitlePopEnabled = true;
        public float resultTitleIntroInDuration = 0.2f;
        public float resultTitleIntroSettleDuration = 0.26f;
        public float resultTitleIntroPeakScale = 1.22f;
        public float resultTitleIntroStartScale = 0.04f;
        [Tooltip("标题弹出后，沿本地 Y 轴上下摇摆（像素，约等于 anchored 空间）。")]
        public bool resultTitleVerticalBobEnabled = true;
        public float resultTitleBobOffsetY = 10f;
        [Tooltip("从一侧摆到另一侧的半周期（秒），与合成界面图标的左右摇周期类似。")]
        public float resultTitleBobHalfPeriod = 0.42f;

        [Tooltip("单抽时在 Grid/布局算出位置后改为父节点中心（避免只占左上角第一格）。关闭则保留布局算出位置。")]
        [SerializeField] private bool centerSingleHatchVisual = true;

        [Tooltip("五连抽发牌动画：全部格子堆叠起点相对 Grid 中心的向上偏移（anchored，单位与 UGUI 一致）。")]
        [SerializeField] private float fivePullDealStackOffsetY = 100f;

        public float slideDuration = 0.42f;
        public float settleDuration = 0.32f;
        public float overshootLeftPx = 72f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        private Sequence _resultIntroSeq;
        private Sequence _resultTitleSeq;
        private int _lastBatch = 1;
        private readonly List<PetDef> _lastResults = new List<PetDef>();
        private readonly List<RectTransform> _resultCellRts = new List<RectTransform>();
        private readonly List<PetHatchPoolConfig> _resolvedPools = new List<PetHatchPoolConfig>();
        Vector3 _resultTitleDesignLocalPos;

        int _poolIndex;
        string _pendingPetHatchPoolId;
        int? _pendingPetHatchCatalogIndex;
        Sequence _poolSwipeSeq;
        Vector2 _poolSlideRestAnchored;

        Sequence _eggIdleSeq;
        Sequence _eggHatchSeq;
        Vector2 _eggIdleRestAnchored;
        Vector3 _eggIdleRestLocalEuler;
        bool _hatchAnimPlaying;
        int _pendingHatchBatch;
        bool _runHatchCommitActive;

        /// <summary>孵化结算：蒙层/空白关闭是否已允许（宠物格子与标题入场播完前为 false）。</summary>
        bool _resultOverlayDismissAllowed = true;
        int _resultOverlayDismissBlockers;

        bool UsesConfigurablePools => _resolvedPools.Count > 0;

        QualityVisualLibrary Lib =>
            qualityVisualLibrary != null ? qualityVisualLibrary : ConfigRegistry.QualityVisuals;

        private void Awake()
        {
            maskCloseButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                if (_hatchAnimPlaying)
                {
                    SkipHatchIntroToCommit();
                    return;
                }

                Hide();
            });
            closeButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                if (_hatchAnimPlaying)
                {
                    SkipHatchIntroToCommit();
                    return;
                }

                Hide();
            });
            probButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                if (probOverlay != null) probOverlay.SetActive(true);
                RefreshProbScroll();
                PlayProbIntroPop();
            });
            probCloseButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                _probIntroSeq?.Kill();
                _probIntroSeq = null;
                if (probOverlay != null) probOverlay.SetActive(false);
            });
            hatchOneButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                DoHatch(1);
            });
            hatchFiveButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                DoHatch(5);
            });
            resultToBackpackButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnGotoBackpack();
            });
            resultHatchAgainButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnHatchAgain();
            });
            resultOverlayMaskCloseButton?.onClick.AddListener(() =>
            {
                if (!_resultOverlayDismissAllowed)
                {
                    AudioGameplayFeedback.PlayUiError();
                    return;
                }

                AudioGameplayFeedback.PlayUiButton();
                CloseResultOverlay();
            });

            poolPrevButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                TryCyclePool(-1);
            });
            poolNextButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                TryCyclePool(1);
            });

            if (resultTitleText != null)
                _resultTitleDesignLocalPos = resultTitleText.rectTransform.localPosition;

            if (poolSlideRoot != null)
                _poolSlideRestAnchored = poolSlideRoot.anchoredPosition;

            EnsureSlideHost();
            HideImmediate();
        }

        private void OnEnable()
        {
            UiCoordinator.Register(this);
            EconomyDisplayEvents.Changed += OnEconomyDisplayChanged;
        }

        private void OnDisable()
        {
            EconomyDisplayEvents.Changed -= OnEconomyDisplayChanged;
            UiCoordinator.Unregister(this);
        }

        void OnEconomyDisplayChanged()
        {
            if (slideHost == null || !slideHost.IsShown) return;
            RefreshCosts();
        }

        void Update()
        {
            if (!_hatchAnimPlaying)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                SkipHatchIntroToCommit();
                return;
            }

            if (Input.touchCount <= 0)
                return;

            for (var i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase != TouchPhase.Began)
                    continue;
                SkipHatchIntroToCommit();
                return;
            }
        }

        private void OnDestroy()
        {
            KillPanelTweens();
            _resultIntroSeq?.Kill();
            _resultTitleSeq?.Kill();
            _probIntroSeq?.Kill();
            _poolSwipeSeq?.Kill();
            _eggIdleSeq?.Kill();
            _eggHatchSeq?.Kill();
            if (poolSlideRoot != null)
                poolSlideRoot.DOKill();
            if (eggIdleAnimRoot != null)
                eggIdleAnimRoot.DOKill();
            KillResultGridTweens();
            KillCrackImageTweens();
            KillResultTitleTweens(resetLocalPose: true);
        }

        Sequence _probIntroSeq;

        public void Open(UiOpenPayload payload = null)
        {
            CapturePetHatchOpenIntent(payload);
            Show();
        }
        public void Close() => Hide();

        public void Show()
        {
            EnsureSlideHost();
            if (resultOverlay != null) resultOverlay.SetActive(false);
            if (probOverlay != null) probOverlay.SetActive(false);

            slideHost.PlayOpen(PopulateAfterOpen);
        }

        void PopulateAfterOpen()
        {
            ResolveCatalogPools();
            ApplyPendingPetHatchPoolSelection();
            _poolIndex = Mathf.Clamp(_poolIndex, 0, Mathf.Max(0, _resolvedPools.Count - 1));
            if (poolSlideRoot != null)
            {
                poolSlideRoot.DOKill();
                poolSlideRoot.anchoredPosition = _poolSlideRestAnchored;
            }

            RefreshCosts();
            RefreshPoolNavButtons();
            CacheEggRestPose();
            RestartEggIdleLoop();
        }

        public void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing) return;
            if (_hatchAnimPlaying)
            {
                SkipHatchIntroToCommit();
                return;
            }

            slideHost.RequestClose(FinishHideAfterSlide);
        }

        void FinishHideAfterSlide()
        {
            _resultIntroSeq?.Kill();
            _resultTitleSeq?.Kill();
            _probIntroSeq?.Kill();
            _poolSwipeSeq?.Kill();
            KillEggAnimSequences();
            if (eggIdleAnimRoot != null)
            {
                eggIdleAnimRoot.DOKill();
                eggIdleAnimRoot.anchoredPosition = _eggIdleRestAnchored;
                eggIdleAnimRoot.localEulerAngles = _eggIdleRestLocalEuler;
            }
            if (poolSlideRoot != null)
            {
                poolSlideRoot.DOKill();
                poolSlideRoot.anchoredPosition = _poolSlideRestAnchored;
            }
            KillCrackImageTweens();
            KillResultGridTweens();
            KillResultTitleTweens(resetLocalPose: true);
            ResetResultOverlayDismissStateForClosedOverlay();
            _hatchAnimPlaying = false;
            _runHatchCommitActive = false;
        }

        private void HideImmediate()
        {
            EnsureSlideHost();
            _resultIntroSeq?.Kill();
            _resultTitleSeq?.Kill();
            _probIntroSeq?.Kill();
            _poolSwipeSeq?.Kill();
            KillEggAnimSequences();
            if (eggIdleAnimRoot != null)
            {
                eggIdleAnimRoot.DOKill();
                eggIdleAnimRoot.anchoredPosition = _eggIdleRestAnchored;
                eggIdleAnimRoot.localEulerAngles = _eggIdleRestLocalEuler;
            }
            if (poolSlideRoot != null)
            {
                poolSlideRoot.DOKill();
                poolSlideRoot.anchoredPosition = _poolSlideRestAnchored;
            }
            KillCrackImageTweens();
            KillResultGridTweens();
            KillResultTitleTweens(resetLocalPose: true);
            ResetResultOverlayDismissStateForClosedOverlay();
            _hatchAnimPlaying = false;
            _runHatchCommitActive = false;
            slideHost.HideImmediate();
        }

        void KillPanelTweens() => slideHost?.KillTweens();

        void EnsureSlideHost()
        {
            UiSlidingPanelHost.EnsureOn(
                this,
                ref slideHost,
                root,
                mainPanel,
                UiSlidingPanelHost.SlideDirection.FromLeft,
                slideDuration,
                settleDuration,
                overshootLeftPx,
                0.55f);
        }

        private void KillResultGridTweens()
        {
            if (resultGrid == null) return;
            foreach (Transform c in resultGrid)
                c.DOKill();
        }

        private void RefreshCosts()
        {
            if (!UsesConfigurablePools)
                RefreshLegacyHatchCosts();
            else
                RefreshConfigurableHatchCosts();

            RefreshPoolNavRedDots();
        }

        void RefreshLegacyHatchCosts()
        {
            if (poolDisplayNameText != null)
                poolDisplayNameText.text = legacyPoolDisplayName;

            ApplyPoolEggSprite(null);

            var one = PetHatchProbability.GetSingleHatchCoinCost();
            var coins = SaveService.Data?.coins ?? 0;
            SetCostText(hatchOneCostText, one, coins);
            SetCostText(hatchFiveCostText, one * 5L, coins);
            if (hatchOneButton != null)
                hatchOneButton.interactable = true;
            if (hatchFiveButton != null)
                hatchFiveButton.interactable = true;
        }

        void RefreshConfigurableHatchCosts()
        {
            var pool = ActivePoolConfig;
            if (pool == null)
                return;

            if (poolDisplayNameText != null)
                poolDisplayNameText.text = pool.displayName;

            ApplyPoolEggSprite(pool);

            ApplyCurrencyIcons(pool);

            var d = SaveService.Data;
            var balance = pool.currency == HatchSpendCurrency.Gems ? (d?.gems ?? 0) : (d?.coins ?? 0);

            SetCostText(hatchOneCostText, pool.singleDrawCost, balance);
            SetCostText(hatchFiveCostText, pool.GetFiveDrawCost(), balance);

            if (hatchOneButton != null)
                hatchOneButton.interactable = true;
            if (hatchFiveButton != null)
                hatchFiveButton.interactable = true;
        }

        PetHatchPoolConfig ActivePoolConfig =>
            UsesConfigurablePools
                ? _resolvedPools[Mathf.Clamp(_poolIndex, 0, _resolvedPools.Count - 1)]
                : null;

        void ResolveCatalogPools()
        {
            _resolvedPools.Clear();
            if (hatchPoolsCatalog?.pools == null)
                return;

            foreach (var p in hatchPoolsCatalog.pools)
            {
                if (p != null)
                    _resolvedPools.Add(p);
            }
        }

        void CapturePetHatchOpenIntent(UiOpenPayload payload)
        {
            _pendingPetHatchPoolId = null;
            _pendingPetHatchCatalogIndex = null;
            var ph = payload?.PetHatch;
            if (ph == null)
                return;
            if (!string.IsNullOrWhiteSpace(ph.PoolId))
                _pendingPetHatchPoolId = ph.PoolId.Trim();
            if (ph.CatalogIndex >= 0)
                _pendingPetHatchCatalogIndex = ph.CatalogIndex;
        }

        void ApplyPendingPetHatchPoolSelection()
        {
            if (_resolvedPools.Count == 0)
            {
                _pendingPetHatchPoolId = null;
                _pendingPetHatchCatalogIndex = null;
                return;
            }

            var applied = false;
            if (!string.IsNullOrEmpty(_pendingPetHatchPoolId))
            {
                for (var i = 0; i < _resolvedPools.Count; i++)
                {
                    var c = _resolvedPools[i];
                    if (c != null && c.poolId == _pendingPetHatchPoolId)
                    {
                        _poolIndex = i;
                        applied = true;
                        break;
                    }
                }

                if (!applied)
                    Debug.LogWarning(
                        $"{nameof(PetHatchPanel)}: 未找到 poolId「{_pendingPetHatchPoolId}」，保持卡池索引 {_poolIndex}。");
            }

            if (!applied && _pendingPetHatchCatalogIndex.HasValue)
                _poolIndex = Mathf.Clamp(_pendingPetHatchCatalogIndex.Value, 0, _resolvedPools.Count - 1);

            _pendingPetHatchPoolId = null;
            _pendingPetHatchCatalogIndex = null;
        }

        void RefreshPoolNavButtons()
        {
            var show = UsesConfigurablePools && _resolvedPools.Count > 1;
            if (poolPrevButton != null)
                poolPrevButton.gameObject.SetActive(show);
            if (poolNextButton != null)
                poolNextButton.gameObject.SetActive(show);
        }

        void TryCyclePool(int delta)
        {
            if (!UsesConfigurablePools || _resolvedPools.Count <= 1)
                return;

            var n = _resolvedPools.Count;
            var newIdx = ((_poolIndex + delta) % n + n) % n;
            if (newIdx == _poolIndex)
                return;

            PlayPoolSwitchAndApply(newIdx, delta);
        }

        void PlayPoolSwitchAndApply(int newIndex, int delta)
        {
            var slideSign = delta > 0 ? -1f : 1f;
            var px = Mathf.Max(40f, Mathf.Abs(poolSwipeDistancePx));

            _poolSwipeSeq?.Kill();
            _poolSwipeSeq = null;

            StopEggIdleLoop();
            ResetEggPoseHard();

            if (poolSlideRoot == null)
            {
                _poolIndex = newIndex;
                FinishPoolSwitchRefresh();
                CacheEggRestPose();
                RestartEggIdleLoop();
                return;
            }

            var rt = poolSlideRoot;
            rt.DOKill();

            var rest = _poolSlideRestAnchored;
            var outPos = rest + new Vector2(slideSign * px, 0f);
            var enterStart = rest + new Vector2(-slideSign * px, 0f);

            _poolSwipeSeq = DOTween.Sequence().SetUpdate(true);
            _poolSwipeSeq.Append(rt.DOAnchorPos(outPos, Mathf.Max(0.02f, poolSwipeHalfDuration)).SetEase(Ease.InQuad))
                .AppendCallback(() =>
                {
                    _poolIndex = newIndex;
                    FinishPoolSwitchRefresh();
                    rt.anchoredPosition = enterStart;
                })
                .Append(rt.DOAnchorPos(rest, Mathf.Max(0.02f, poolSwipeHalfDuration)).SetEase(Ease.OutQuad));
            _poolSwipeSeq.OnComplete(() =>
            {
                CacheEggRestPose();
                RestartEggIdleLoop();
            });
        }

        void FinishPoolSwitchRefresh()
        {
            RefreshCosts();
            RefreshPoolNavButtons();
            if (probOverlay != null && probOverlay.activeSelf)
                RefreshProbScroll();
        }

        void ApplyCurrencyIcons(PetHatchPoolConfig pool)
        {
            if (pool == null)
                return;

            var sp = pool.currencyIcon;
            if (sp == null)
                sp = pool.currency == HatchSpendCurrency.Gems ? fallbackGemCurrencySprite : fallbackCoinCurrencySprite;

            if (sp == null)
                return;

            if (hatchOneCurrencyIcon != null)
            {
                hatchOneCurrencyIcon.sprite = sp;
                hatchOneCurrencyIcon.enabled = true;
            }

            if (hatchFiveCurrencyIcon != null)
            {
                hatchFiveCurrencyIcon.sprite = sp;
                hatchFiveCurrencyIcon.enabled = true;
            }
        }

        void RefreshResultHatchAgainInteractable()
        {
            if (resultHatchAgainButton == null)
                return;
            if (SaveService.Data == null)
                return;

            resultHatchAgainButton.interactable = PetService.CanAddToInventory(_lastBatch);
        }

        private static void SetCostText(Text t, long cost, long balance)
        {
            if (t == null) return;
            t.text = $"x{cost}";
            t.color = balance >= cost ? Color.white : new Color(0.94f, 0.22f, 0.2f, 1f);
        }

        private void RefreshProbScroll()
        {
            if (probContent == null || probRowPrefab == null)
            {
                if (probContent != null && probRowPrefab == null)
                    Debug.LogError($"{nameof(PetHatchPanel)}: 请在 Inspector 指定 {nameof(probRowPrefab)}。", this);
                return;
            }

            for (var i = probContent.childCount - 1; i >= 0; i--)
                Destroy(probContent.GetChild(i).gameObject);

            List<PetHatchProbability.Row> rows;
            if (UsesConfigurablePools && ActivePoolConfig != null)
                rows = PetHatchProbability.BuildRowsForPool(ActivePoolConfig, out _);
            else
                rows = PetHatchProbability.BuildRows(out _);

            var lib = Lib;

            if (rows.Count == 0)
                return;

            foreach (var r in rows)
            {
                if (r.Def == null || string.IsNullOrWhiteSpace(r.Def.petId))
                    continue;

                var rowView = Instantiate(probRowPrefab, probContent);
                rowView.ApplyProbabilityPercent(r.Def.petId, lib, r.Probability01);
            }

            Canvas.ForceUpdateCanvases();
            if (probScroll != null)
                probScroll.verticalNormalizedPosition = 1f;
        }

        private void PlayProbIntroPop()
        {
            PlayPanelIntroPop(ref _probIntroSeq, probPopAnimRoot, true, resultIntroInDuration,
                resultIntroSettleDuration, resultIntroPeakScale, resultIntroStartScale);
        }

        private void DoHatch(int batch)
        {
            if (_hatchAnimPlaying)
                return;

            _lastBatch = batch;
            _lastResults.Clear();

            var d = SaveService.Data;
            if (d == null)
                return;

            if (UsesConfigurablePools)
            {
                var pool = ActivePoolConfig;
                if (pool == null)
                    return;

                if (batch == 1)
                {
                    var cost = pool.singleDrawCost;
                    var balance = pool.currency == HatchSpendCurrency.Gems ? d.gems : d.coins;
                    if (cost > 0 && balance < cost)
                    {
                        if (pool.currency == HatchSpendCurrency.Gems)
                            UiRouter.OpenFreeDiamondsOffer();
                        else
                            UiRouter.OpenFreeCoinsOffer();
                        return;
                    }

                    if (!PetService.CanAddToInventory(1))
                    {
                        ShowPetInventoryFullHud();
                        return;
                    }
                }
                else
                {
                    var total = pool.GetFiveDrawCost();
                    var balance = pool.currency == HatchSpendCurrency.Gems ? d.gems : d.coins;
                    if (balance < total)
                    {
                        if (pool.currency == HatchSpendCurrency.Gems)
                            UiRouter.OpenFreeDiamondsOffer();
                        else
                            UiRouter.OpenFreeCoinsOffer();
                        return;
                    }

                    if (!PetService.CanAddToInventory(5))
                    {
                        ShowPetInventoryFullHud();
                        return;
                    }
                }
            }
            else
            {
                if (batch == 1)
                {
                    var cost = PetHatchService.GetEstimatedSingleHatchCostCoins();
                    if (cost > 0 && d.coins < cost)
                    {
                        UiRouter.OpenFreeCoinsOffer();
                        return;
                    }

                    if (!PetService.CanAddToInventory(1))
                    {
                        ShowPetInventoryFullHud();
                        return;
                    }
                }
                else
                {
                    var costOne = PetHatchService.GetEstimatedSingleHatchCostCoins();
                    var total = (long)costOne * 5L;
                    if (d.coins < total)
                    {
                        UiRouter.OpenFreeCoinsOffer();
                        return;
                    }

                    if (!PetService.CanAddToInventory(5))
                    {
                        ShowPetInventoryFullHud();
                        return;
                    }
                }
            }

            if (!hatchIntroFxEnabled || !CanPlayHatchIntroFx())
            {
                AudioManager.Instance?.PlaySfx(SfxId.HatchStart);
                RunHatchCommit(batch);
                return;
            }

            _hatchAnimPlaying = true;
            SetHatchButtonsLocked(true);
            StopEggIdleLoop();
            ResetEggPoseHard();
            ResetEggCracksVisual();
            PlayHatchIntroPresentation(batch);
        }

        void SkipHatchIntroToCommit()
        {
            if (!_hatchAnimPlaying)
                return;

            KillEggAnimSequences();
            RunHatchCommit(_pendingHatchBatch);
        }

        void ShowPetInventoryFullHud()
        {
            AudioGameplayFeedback.PlayUiError();
            HUDMgr.ShowHUD(new HUDMessage
            {
                Title = "宠物背包已满",
                Content = "宠物背包已满，无法进行孵化。",
                BtnShow = true,
                ConfirmText = "前往背包",
                CancelText = "关闭",
                CloseOnConfirm = true,
                CloseOnCancel = true,
                ConfirmAction = () =>
                {
                    Hide();
                    UiRouter.OpenPet();
                }
            });
        }

        void RunHatchCommit(int batch)
        {
            if (_runHatchCommitActive)
                return;

            _runHatchCommitActive = true;
            _hatchAnimPlaying = false;
            SetHatchButtonsLocked(false);

            try
            {
                bool ok;
                if (UsesConfigurablePools)
                {
                    var pool = ActivePoolConfig;
                    if (pool == null)
                    {
                        AudioGameplayFeedback.PlayUiError();
                        RestartEggIdleLoop();
                        return;
                    }

                    if (batch == 1)
                    {
                        ok = PetHatchService.TryHatchOnce(pool, out var p) && p != null;
                        if (ok) _lastResults.Add(p);
                    }
                    else
                    {
                        ok = PetHatchService.TryHatchFive(pool, out var list) && list != null && list.Count > 0;
                        if (ok) _lastResults.AddRange(list);
                    }
                }
                else
                {
                    if (batch == 1)
                    {
                        ok = PetHatchService.TryHatchOnce(out var p) && p != null;
                        if (ok) _lastResults.Add(p);
                    }
                    else
                    {
                        ok = PetHatchService.TryHatchFive(out var list) && list != null && list.Count > 0;
                        if (ok) _lastResults.AddRange(list);
                    }
                }

                if (!ok)
                {
                    AudioGameplayFeedback.PlayUiError();
                    ResetEggCracksVisual();
                    RestartEggIdleLoop();
                    return;
                }

                AudioManager.Instance?.PlaySfx(SfxId.PetObtainHatch);
                SaveService.Save();
                ShowResult();
                RefreshCosts();
                ResetEggCracksVisual();
                ResetEggPoseHard();
                StopEggIdleLoop();
            }
            finally
            {
                _runHatchCommitActive = false;
            }
        }

        bool CanPlayHatchIntroFx()
        {
            if (eggIdleAnimRoot == null)
                return false;
            return eggCrackFrag1 != null && eggCrackFrag2 != null && eggCrackFrag3 != null;
        }

        void SetHatchButtonsLocked(bool locked)
        {
            if (!locked)
            {
                RefreshCosts();
                return;
            }

            if (hatchOneButton != null) hatchOneButton.interactable = false;
            if (hatchFiveButton != null) hatchFiveButton.interactable = false;
        }

        void KillEggAnimSequences()
        {
            _eggIdleSeq?.Kill();
            _eggIdleSeq = null;
            _eggHatchSeq?.Kill();
            _eggHatchSeq = null;
        }

        void KillCrackImageTweens()
        {
            eggCrackFrag1?.DOKill();
            eggCrackFrag2?.DOKill();
            eggCrackFrag3?.DOKill();
        }

        void CacheEggRestPose()
        {
            if (eggIdleAnimRoot == null)
                return;
            _eggIdleRestAnchored = eggIdleAnimRoot.anchoredPosition;
            _eggIdleRestLocalEuler = eggIdleAnimRoot.localEulerAngles;
        }

        void ResetEggPoseHard()
        {
            if (eggIdleAnimRoot == null)
                return;
            eggIdleAnimRoot.DOKill();
            eggIdleAnimRoot.anchoredPosition = _eggIdleRestAnchored;
            eggIdleAnimRoot.localEulerAngles = _eggIdleRestLocalEuler;
        }

        void StopEggIdleLoop()
        {
            _eggIdleSeq?.Kill();
            _eggIdleSeq = null;
            if (eggIdleAnimRoot != null)
                eggIdleAnimRoot.DOKill();
        }

        void RestartEggIdleLoop()
        {
            if (slideHost == null || !slideHost.IsShown || eggIdleAnimRoot == null || _hatchAnimPlaying)
                return;

            StopEggIdleLoop();
            ResetEggPoseHard();

            var g = eggIdleAnimRoot;
            var tilt = Mathf.Clamp(eggIdleTiltDegrees, 1f, 25f);
            var hop = Mathf.Clamp(eggIdleHopAnchoredPx, 1f, 48f);
            var s1 = Mathf.Max(0.08f, eggIdleSwayHalfSec);
            var sj = Mathf.Max(0.06f, eggIdleFastJitterHalfSec);
            var pause = Mathf.Max(0f, eggIdlePauseBetweenCyclesSec);

            _eggIdleSeq = DOTween.Sequence().SetUpdate(true).SetLoops(-1);
            _eggIdleSeq.Append(g.DOLocalRotate(new Vector3(0f, 0f, -tilt), s1 * 1.2f).SetEase(Ease.InOutSine));
            _eggIdleSeq.Append(g.DOLocalRotate(new Vector3(0f, 0f, tilt), s1 * 2.2f).SetEase(Ease.InOutSine));
            _eggIdleSeq.Append(g.DOLocalRotate(Vector3.zero, s1 * 1f).SetEase(Ease.InOutSine));
            _eggIdleSeq.Append(
                g.DOAnchorPosY(_eggIdleRestAnchored.y + hop, s1 * 0.85f).SetEase(Ease.OutQuad));
            _eggIdleSeq.Append(
                g.DOAnchorPosY(_eggIdleRestAnchored.y, s1 * 0.85f).SetEase(Ease.InQuad));
            _eggIdleSeq.Append(g.DOLocalRotate(new Vector3(0f, 0f, -tilt * 1.55f), sj).SetEase(Ease.InOutSine));
            _eggIdleSeq.Append(g.DOLocalRotate(new Vector3(0f, 0f, tilt * 1.55f), sj * 2f).SetEase(Ease.InOutSine));
            _eggIdleSeq.Append(g.DOLocalRotate(new Vector3(0f, 0f, -tilt * 0.9f), sj * 0.9f).SetEase(Ease.InOutSine));
            _eggIdleSeq.Append(g.DOLocalRotate(Vector3.zero, sj * 0.9f).SetEase(Ease.OutQuad));
            _eggIdleSeq.Append(
                g.DOAnchorPosY(_eggIdleRestAnchored.y + hop * 1.35f, s1 * 0.45f).SetEase(Ease.OutQuad));
            _eggIdleSeq.Append(
                g.DOAnchorPosY(_eggIdleRestAnchored.y, s1 * 0.5f).SetEase(Ease.InOutQuad));
            if (pause > 0.001f)
                _eggIdleSeq.AppendInterval(pause);
        }

        void ResetEggCracksVisual()
        {
            KillCrackImageTweens();
            foreach (var img in CrackImages())
            {
                if (img == null) continue;
                img.gameObject.SetActive(false);
                var c = img.color;
                c.a = 0f;
                img.color = c;
            }
        }

        IEnumerable<Image> CrackImages()
        {
            yield return eggCrackFrag1;
            yield return eggCrackFrag2;
            yield return eggCrackFrag3;
        }

        void PlayHatchIntroPresentation(int batch)
        {
            KillEggAnimSequences();
            KillCrackImageTweens();
            _pendingHatchBatch = batch;
            AudioManager.Instance?.PlaySfx(SfxId.HatchStart);

            var g = eggIdleAnimRoot;
            ResetEggPoseHard();

            var seq = DOTween.Sequence().SetUpdate(true);
            _eggHatchSeq = seq;

            seq.Append(g.DOLocalRotate(_eggIdleRestLocalEuler, 0.2f).SetEase(Ease.OutQuad));
            seq.Join(g.DOAnchorPos(_eggIdleRestAnchored, 0.2f).SetEase(Ease.OutQuad));

            var nShake = Mathf.Clamp(preCrackSharpShakeCount, 1, 6);
            var step = Mathf.Max(0.03f, preCrackSharpShakeStepSec);
            var tilt = Mathf.Clamp(preCrackSharpTiltDeg, 4f, 40f);
            for (var i = 0; i < nShake; i++)
            {
                seq.Append(g.DOLocalRotate(_eggIdleRestLocalEuler + new Vector3(0f, 0f, -tilt), step)
                    .SetEase(Ease.InOutSine));
                seq.Append(g.DOLocalRotate(_eggIdleRestLocalEuler + new Vector3(0f, 0f, tilt), step)
                    .SetEase(Ease.InOutSine));
            }

            seq.Append(g.DOLocalRotate(_eggIdleRestLocalEuler, 0.12f).SetEase(Ease.OutQuad));

            Image[] cracks = { eggCrackFrag1, eggCrackFrag2, eggCrackFrag3 };
            var fade = Mathf.Max(0.08f, crackLayerFadeSec);
            foreach (var crack in cracks)
            {
                if (crack == null)
                    continue;
                var cRef = crack;
                seq.AppendCallback(() =>
                {
                    cRef.gameObject.SetActive(true);
                    var col = cRef.color;
                    col.a = 0f;
                    cRef.color = col;
                });
                seq.Append(cRef.DOFade(1f, fade).SetEase(Ease.OutQuad));
                seq.Append(g.DOPunchRotation(
                    new Vector3(0f, 0f, Mathf.Clamp(eggShakeAfterCrackPunchStrengthZ, 4f, 60f)),
                    Mathf.Max(0.08f, eggShakeAfterCrackPunchDuration),
                    Mathf.Clamp(eggShakeAfterCrackPunchVibrato, 4, 30),
                    1f));
            }

            seq.Append(g.DOLocalRotate(_eggIdleRestLocalEuler, 0.12f).SetEase(Ease.OutQuad));
            seq.OnComplete(() =>
            {
                _eggHatchSeq = null;
                RunHatchCommit(batch);
            });
        }

        void ApplyPoolEggSprite(PetHatchPoolConfig pool)
        {
            if (eggIconImage == null)
                return;

            var sp = pool != null ? pool.eggIcon : null;
            if (sp == null)
                sp = defaultEggSprite;
            if (sp == null)
                return;

            eggIconImage.sprite = sp;
            eggIconImage.enabled = true;
        }

        void RefreshPoolNavRedDots()
        {
            if (poolPrevRedDot != null)
                poolPrevRedDot.SetActive(false);
            if (poolNextRedDot != null)
                poolNextRedDot.SetActive(false);

            if (!UsesConfigurablePools || _resolvedPools.Count <= 1)
                return;

            var n = _resolvedPools.Count;
            var prevIdx = (_poolIndex - 1 + n) % n;
            var nextIdx = (_poolIndex + 1) % n;

            if (poolPrevRedDot != null)
                poolPrevRedDot.SetActive(CanAffordSingleDrawInPool(_resolvedPools[prevIdx]));
            if (poolNextRedDot != null)
                poolNextRedDot.SetActive(CanAffordSingleDrawInPool(_resolvedPools[nextIdx]));
        }

        static bool CanAffordSingleDrawInPool(PetHatchPoolConfig p)
        {
            if (p == null)
                return false;
            var d = SaveService.Data;
            if (d == null)
                return false;
            if (!PetService.CanAddToInventory(1))
                return false;
            var bal = p.currency == HatchSpendCurrency.Gems ? d.gems : d.coins;
            return bal >= p.singleDrawCost;
        }

        private void ShowResult()
        {
            KillResultGridTweens();
            _resultIntroSeq?.Kill();
            _resultTitleSeq?.Kill();
            KillResultTitleTweens(resetLocalPose: false);

            BeginResultOverlayDismissGating();

            if (resultOverlay != null) resultOverlay.SetActive(true);
            if (resultTitleText != null) resultTitleText.text = "孵化成功!";
            HideResultTitleForSlotPhase();

            if (resultPopAnimRoot != null)
                resultPopAnimRoot.localScale = Vector3.one;

            if (resultHatchAgainLabel != null)
                resultHatchAgainLabel.text = _lastBatch == 5 ? "再孵5颗" : "再孵1颗";

            RefreshResultHatchAgainInteractable();

            RegisterResultOverlayDismissBlocker();
            RegisterResultOverlayDismissBlocker();

            if (resultGrid == null || hatchResultRowPrefab == null)
            {
                if (resultGrid != null && hatchResultRowPrefab == null)
                {
                    Debug.LogError($"{nameof(PetHatchPanel)}: 请在 Inspector 指定 {nameof(hatchResultRowPrefab)}。", this);
                }

                PlayPanelIntroPop(ref _resultIntroSeq, resultPopAnimRoot, resultIntroPopEnabled, resultIntroInDuration,
                    resultIntroSettleDuration, resultIntroPeakScale, resultIntroStartScale, ReleaseResultOverlayDismissBlocker);
                PlayResultTitleRevealAndBob(ReleaseResultOverlayDismissBlocker);
                return;
            }

            foreach (Transform c in resultGrid)
                Destroy(c.gameObject);
            _resultCellRts.Clear();

            var lib = Lib;

            foreach (var p in _lastResults)
            {
                if (p == null) continue;
                var cell = Instantiate(hatchResultRowPrefab, resultGrid);
                cell.ApplyDisplayOnly(p.petId, lib);
                _resultCellRts.Add(cell.GetComponent<RectTransform>());
            }

            Canvas.ForceUpdateCanvases();

            PlayPanelIntroPop(ref _resultIntroSeq, resultPopAnimRoot, resultIntroPopEnabled, resultIntroInDuration,
                resultIntroSettleDuration, resultIntroPeakScale, resultIntroStartScale, ReleaseResultOverlayDismissBlocker);
            AnimateHatchResultSlots();
        }

        void BeginResultOverlayDismissGating()
        {
            _resultOverlayDismissBlockers = 0;
            SetResultOverlayDismissAllowed(false);
        }

        void RegisterResultOverlayDismissBlocker() => _resultOverlayDismissBlockers++;

        void ReleaseResultOverlayDismissBlocker()
        {
            if (_resultOverlayDismissBlockers <= 0) return;
            _resultOverlayDismissBlockers--;
            if (_resultOverlayDismissBlockers <= 0)
            {
                _resultOverlayDismissBlockers = 0;
                SetResultOverlayDismissAllowed(true);
            }
        }

        void SetResultOverlayDismissAllowed(bool allowed)
        {
            _resultOverlayDismissAllowed = allowed;
            if (resultOverlayMaskCloseButton != null)
                resultOverlayMaskCloseButton.interactable = allowed;
        }

        void ResetResultOverlayDismissStateForClosedOverlay()
        {
            _resultOverlayDismissBlockers = 0;
            SetResultOverlayDismissAllowed(true);
        }

        RectTransform ResultTitleRect =>
            resultTitleText != null ? resultTitleText.rectTransform : null;

        void HideResultTitleForSlotPhase()
        {
            var rt = ResultTitleRect;
            if (rt == null) return;
            rt.DOKill();
            rt.localPosition = _resultTitleDesignLocalPos;
            rt.localScale = Vector3.zero;
        }

        void KillResultTitleTweens(bool resetLocalPose)
        {
            _resultTitleSeq?.Kill();
            _resultTitleSeq = null;
            var rt = ResultTitleRect;
            if (rt == null) return;
            rt.DOKill();
            if (!resetLocalPose)
                return;
            rt.localScale = Vector3.one;
            rt.localPosition = _resultTitleDesignLocalPos;
        }

        void PlayResultTitleRevealAndBob(System.Action onPresented = null)
        {
            var rt = ResultTitleRect;
            if (rt == null)
            {
                onPresented?.Invoke();
                return;
            }

            _resultTitleSeq?.Kill();
            _resultTitleSeq = null;
            rt.DOKill();
            rt.localScale = Vector3.zero;

            void DonePresented()
            {
                onPresented?.Invoke();
            }

            void StartVerticalBob()
            {
                if (!resultTitleVerticalBobEnabled || resultTitleBobHalfPeriod <= 0.02f)
                    return;
                var px = Mathf.Clamp(resultTitleBobOffsetY, 1f, 96f);
                var p = rt.localPosition;
                p.y = _resultTitleDesignLocalPos.y;
                rt.localPosition = p;
                var y0 = _resultTitleDesignLocalPos.y;
                rt.DOLocalMoveY(y0 + px, resultTitleBobHalfPeriod)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }

            if (!resultTitlePopEnabled)
            {
                rt.localScale = Vector3.one;
                StartVerticalBob();
                DonePresented();
                return;
            }

            var start = Mathf.Clamp(resultTitleIntroStartScale, 0f, 0.95f);
            var peak = Mathf.Max(1f, resultTitleIntroPeakScale);
            rt.localScale = Vector3.one * start;

            _resultTitleSeq = DOTween.Sequence().SetUpdate(true);
            _resultTitleSeq.Append(rt.DOScale(peak, Mathf.Max(0.05f, resultTitleIntroInDuration))
                .SetEase(Ease.OutCubic));
            _resultTitleSeq.Append(rt.DOScale(1f, Mathf.Max(0.05f, resultTitleIntroSettleDuration))
                .SetEase(Ease.OutElastic, 0.85f, 0.55f));
            _resultTitleSeq.OnComplete(() =>
            {
                StartVerticalBob();
                DonePresented();
            });
        }

        private void AnimateHatchResultSlots()
        {
            if (resultGrid == null || _resultCellRts.Count == 0)
            {
                PlayResultTitleRevealAndBob(ReleaseResultOverlayDismissBlocker);
                return;
            }

            var grid = resultGrid.GetComponent<GridLayoutGroup>();
            var hlg = resultGrid.GetComponent<HorizontalLayoutGroup>();
            var n = _resultCellRts.Count;

            var endAnchored = new Vector2[n];
            var fiveDeal = _lastBatch == 5 && n == 5 && grid != null;

            if (fiveDeal)
            {
                if (hlg != null) hlg.enabled = false;
                grid.enabled = false;

                foreach (var rt in _resultCellRts)
                    PrepCellForDealAnimation(rt, grid.cellSize);

                if (!TryComputeGridEndsAnchoredCenter(grid, resultGrid, n, endAnchored))
                    FallbackSnapshotGridEndsAnchored(grid, hlg, resultGrid, _resultCellRts, endAnchored);

                RunDealTweens(new Vector2(0f, fivePullDealStackOffsetY), endAnchored, grid, hlg,
                    () => PlayResultTitleRevealAndBob(ReleaseResultOverlayDismissBlocker));
                return;
            }

            if (grid != null) grid.enabled = true;
            if (hlg != null) hlg.enabled = true;

            LayoutRebuilder.ForceRebuildLayoutImmediate(resultGrid);
            Canvas.ForceUpdateCanvases();

            for (var i = 0; i < n; i++)
            {
                var rt = _resultCellRts[i];
                endAnchored[i] = rt != null ? rt.anchoredPosition : Vector2.zero;
            }

            if (grid == null && hlg == null)
            {
                var w = Mathf.Max(1f, _resultCellRts[0].rect.width);
                const float spacing = 14f;
                var total = n * w + Mathf.Max(0, n - 1) * spacing;
                var x0 = -total * 0.5f + w * 0.5f;
                for (var i = 0; i < n; i++)
                    endAnchored[i] = new Vector2(x0 + i * (w + spacing), 0f);
            }
            else if (centerSingleHatchVisual && n == 1)
                endAnchored[0] = Vector2.zero;

            if (grid != null) grid.enabled = false;
            if (hlg != null) hlg.enabled = false;

            RunDealTweens(Vector2.zero, endAnchored, grid, hlg,
                () => PlayResultTitleRevealAndBob(ReleaseResultOverlayDismissBlocker));
        }

        void RunDealTweens(Vector2 startAnchored, Vector2[] endAnchored, GridLayoutGroup grid,
            HorizontalLayoutGroup hlg, System.Action onAllDealt = null)
        {
            var n = endAnchored.Length;
            var pending = 0;

            void FinishDeal()
            {
                RestoreHatchResultLayout(grid, hlg);
                onAllDealt?.Invoke();
            }

            for (var i = 0; i < n; i++)
            {
                var rt = _resultCellRts[i];
                if (rt == null) continue;
                rt.DOKill();
                var end = endAnchored[i];
                var needsMove = (end - startAnchored).sqrMagnitude >= 0.0001f;
                if (!needsMove)
                {
                    rt.anchoredPosition = end;
                    continue;
                }

                rt.anchoredPosition = startAnchored;
                pending++;
                var idx = i;
                rt.DOAnchorPos(end, resultCellSlideDuration)
                    .SetEase(Ease.OutCubic)
                    .SetDelay(resultCellStaggerSec * idx)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        pending--;
                        if (pending <= 0)
                            FinishDeal();
                    });
            }

            if (pending == 0)
                FinishDeal();
        }

        static void PrepCellForDealAnimation(RectTransform rt, Vector2 cellSize)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = cellSize;
        }

        /// <summary>
        /// 复刻 Unity <see cref="GridLayoutGroup"/> 在 pivot 居中、左上锚点对齐规则下的格子中心点在父 Rect 局部的位置，
        /// 并换算为同一父节点下子物体 anchor (0.5,0.5) 时的 anchoredPosition（父 pivot 居中时即相对父矩形中心）。
        /// </summary>
        static bool TryComputeGridEndsAnchoredCenter(GridLayoutGroup grid, RectTransform gridRt, int childCount,
            IList<Vector2> ends)
        {
            if (grid == null || gridRt == null || ends == null || childCount <= 0)
                return false;
            if (grid.constraint == GridLayoutGroup.Constraint.Flexible)
                return false;

            float width = gridRt.rect.width;
            float height = gridRt.rect.height;
            Vector2 cellSize = grid.cellSize;
            Vector2 spacing = grid.spacing;
            RectOffset pad = grid.padding;

            int cellCountX = 1;
            int cellCountY = 1;

            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                cellCountX = grid.constraintCount;
                cellCountY = childCount / cellCountX + (childCount % cellCountX > 0 ? 1 : 0);
            }
            else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                cellCountY = grid.constraintCount;
                cellCountX = childCount / cellCountY + (childCount % cellCountY > 0 ? 1 : 0);
            }

            int cornerX = (int)grid.startCorner % 2;
            int cornerY = (int)grid.startCorner / 2;

            int cellsPerMainAxis;
            int actualCellCountX;
            int actualCellCountY;

            if (grid.startAxis == GridLayoutGroup.Axis.Horizontal)
            {
                cellsPerMainAxis = cellCountX;
                actualCellCountX = Mathf.Clamp(cellCountX, 1, childCount);
                actualCellCountY = Mathf.Clamp(cellCountY, 1,
                    Mathf.CeilToInt(childCount / (float)cellsPerMainAxis));
            }
            else
            {
                cellsPerMainAxis = cellCountY;
                actualCellCountY = Mathf.Clamp(cellCountY, 1, childCount);
                actualCellCountX = Mathf.Clamp(cellCountX, 1,
                    Mathf.CeilToInt(childCount / (float)cellsPerMainAxis));
            }

            var requiredSpace = new Vector2(
                actualCellCountX * cellSize.x + Mathf.Max(0, actualCellCountX - 1) * spacing.x,
                actualCellCountY * cellSize.y + Mathf.Max(0, actualCellCountY - 1) * spacing.y
            );

            float startOffsetX =
                HatchGridStartOffset(grid.childAlignment, pad, width, height, 0, requiredSpace.x);
            float startOffsetY =
                HatchGridStartOffset(grid.childAlignment, pad, width, height, 1, requiredSpace.y);

            var topLeft = new Vector2(gridRt.rect.xMin, gridRt.rect.yMax);

            for (var i = 0; i < childCount; i++)
            {
                int positionX;
                int positionY;
                if (grid.startAxis == GridLayoutGroup.Axis.Horizontal)
                {
                    positionX = i % cellsPerMainAxis;
                    positionY = i / cellsPerMainAxis;
                }
                else
                {
                    positionX = i / cellsPerMainAxis;
                    positionY = i % cellsPerMainAxis;
                }

                if (cornerX == 1) positionX = actualCellCountX - 1 - positionX;
                if (cornerY == 1) positionY = actualCellCountY - 1 - positionY;

                float posXCell = startOffsetX + (cellSize.x + spacing.x) * positionX;
                float posYCell = startOffsetY + (cellSize.y + spacing.y) * positionY;

                const float pivotX = 0.5f;
                const float pivotY = 0.5f;
                float gx = posXCell + cellSize.x * pivotX;
                float gy = -posYCell - cellSize.y * (1f - pivotY);

                ends[i] = topLeft + new Vector2(gx, gy);
            }

            return true;
        }

        static float HatchGridStartOffset(TextAnchor childAlignment, RectOffset pad, float width, float height,
            int axis, float requiredSpaceWithoutPadding)
        {
            float requiredSpace = requiredSpaceWithoutPadding + (axis == 0 ? pad.horizontal : pad.vertical);
            float availableSpace = axis == 0 ? width : height;
            float surplusSpace = availableSpace - requiredSpace;
            float alignmentOnAxis = axis == 0
                ? ((int)childAlignment % 3) * 0.5f
                : ((int)childAlignment / 3) * 0.5f;
            return (axis == 0 ? pad.left : pad.top) + surplusSpace * alignmentOnAxis;
        }

        static void FallbackSnapshotGridEndsAnchored(GridLayoutGroup grid, HorizontalLayoutGroup hlg,
            RectTransform gridRt, List<RectTransform> cells, IList<Vector2> ends)
        {
            if (grid == null || gridRt == null) return;
            grid.enabled = true;
            if (hlg != null) hlg.enabled = false;

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRt);
            Canvas.ForceUpdateCanvases();

            for (var i = 0; i < cells.Count; i++)
            {
                var rt = cells[i];
                if (rt == null) continue;
                var wpos = rt.position;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = grid.cellSize;
                rt.position = wpos;
                ends[i] = rt.anchoredPosition;
            }

            grid.enabled = false;
        }

        void RestoreHatchResultLayout(GridLayoutGroup grid, HorizontalLayoutGroup hlg)
        {
            if (grid != null && hlg != null)
                hlg.enabled = false;
            if (grid != null)
                grid.enabled = true;
            else if (hlg != null)
                hlg.enabled = true;
            if (resultGrid != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(resultGrid);
        }

        private static void PlayPanelIntroPop(ref Sequence seqSlot, RectTransform introRt, bool popEnabled,
            float inDuration, float settleDuration, float peakScale, float startScale, Action onComplete = null)
        {
            seqSlot?.Kill();
            seqSlot = null;
            if (introRt == null)
            {
                onComplete?.Invoke();
                return;
            }

            introRt.DOKill();

            if (!popEnabled)
            {
                introRt.localScale = Vector3.one;
                onComplete?.Invoke();
                return;
            }

            var start = Mathf.Clamp(startScale, 0f, 0.95f);
            var peak = Mathf.Max(1f, peakScale);
            introRt.localScale = Vector3.one * start;

            seqSlot = DOTween.Sequence().SetUpdate(true);
            seqSlot.Append(introRt
                .DOScale(peak, Mathf.Max(0.05f, inDuration))
                .SetEase(Ease.OutCubic));
            seqSlot.Append(introRt
                .DOScale(1f, Mathf.Max(0.05f, settleDuration))
                .SetEase(Ease.OutElastic, 0.85f, 0.55f));
            if (onComplete != null)
                seqSlot.OnComplete(() => onComplete.Invoke());
        }

        private void OnGotoBackpack()
        {
            Hide();
            UiRouter.OpenPet();
        }

        /// <summary>仅关闭孵化结算弹窗，回到主孵化界面。</summary>
        private void CloseResultOverlay()
        {
            _resultIntroSeq?.Kill();
            _resultTitleSeq?.Kill();
            KillResultGridTweens();
            KillResultTitleTweens(resetLocalPose: true);
            ResetResultOverlayDismissStateForClosedOverlay();
            if (resultOverlay != null) resultOverlay.SetActive(false);
            ResetEggCracksVisual();
            ResetEggPoseHard();
            RestartEggIdleLoop();
        }

        private void OnHatchAgain()
        {
            if (resultOverlay != null) resultOverlay.SetActive(false);
            DoHatch(_lastBatch);
        }

    }
}
