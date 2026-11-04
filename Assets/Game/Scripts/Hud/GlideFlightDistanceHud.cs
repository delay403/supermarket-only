using System.Collections.Generic;
using DG.Tweening;
using Game.Collectibles.Stats;
using Game.Economy;
using Game.Glide;
using Game.Core;
using Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hud
{
    /// <summary>
    /// 滑翔阶段 UGUI：位图字显示当前水平滑翔距离（节流刷新）、阈值达成时放大/振动，<see cref="UnityEngine.UI.Text"/> 显示与当前距离对应的结算金币预览。
    /// </summary>
    public sealed class GlideFlightDistanceHud : MonoBehaviour
    {
        [Header("References (optional if auto-find)")]
        [SerializeField] private GlideController glide;
        [SerializeField] private StatsAggregator stats;

        [Header("Root")]
        [Tooltip("滑翔时显示、非滑翔时关闭的父物体。")]
        [SerializeField] private GameObject hudRoot;

        [Header("位图字 · 0~9")]
        [Tooltip("索引 0～9 对应数字贴图，与 Resources/UI/Shuzi 等一致。")]
        [SerializeField] private Sprite[] digitSprites = new Sprite[10];

        [Tooltip("从左到右每个子物体一个 Image（仅直接子级）。留空则自动在 HudRoot 下创建 DigitStrip。")]
        [SerializeField] private RectTransform digitStrip;

        [Tooltip("未指定 Digit Strip、且 HudRoot 下无 DigitStrip 子物体时，自动创建的位数格子数量。")]
        [SerializeField] private int autoDigitSlotCount = 8;

        [Tooltip("自动创建时每个数字格子的推荐尺寸（像素）。")]
        [SerializeField] private Vector2 autoDigitCellSize = new Vector2(72f, 96f);

        [SerializeField] private float autoDigitSpacing = 4f;

        [Header("金币预览（UGUI Text）")]
        [SerializeField] private Text coinRewardText;
        [SerializeField] private string coinRewardFormat = "你将获得: {0}金币";

        [Header("刷新")]
        [Tooltip("距离/金币 UI 更新最短间隔（秒），避免每帧改图。")]
        [SerializeField] private float minRefreshIntervalSeconds = 0.08f;

        [Header("距离阈值（米）")]
        [Tooltip("整数米；首次达到或超过时触发一次 pulse（同一趟滑翔各阈值只触发一次）。")]
        [SerializeField] private float[] milestoneDistancesMeters =
        {
            100f, 500f, 1000f, 5000f, 10000f,
        };

        [Header("阈值反馈（DOTween）")]
        [Tooltip("振动/缩放目标；默认同 DigitStrip。")]
        [SerializeField] private RectTransform pulseTarget;
        [Tooltip("相对「本段动画开始前」的缩放倍数：先冲到峰值（振动与其同时进行）。")]
        [SerializeField] private float milestonePulsePeakMultiplier = 1.4f;
        [Tooltip("相对「本段动画开始前」的缩放倍数：峰值回落后的常驻大小；多次阈值后在前一档基础上再乘，可叠很高。")]
        [SerializeField] private float milestonePulseSettleMultiplier = 1.25f;
        [SerializeField] private float pulseDuration = 0.28f;
        [SerializeField] private float punchVibrationStrength = 6f;
        [SerializeField] private int punchVibrato = 6;

        [Header("里程碑音效")]
        [Tooltip("播放 AudioManager 表中 SfxId.GlideDistanceMilestone；关闭则仅动画。")]
        [SerializeField] private bool playMilestoneSound = true;
        [SerializeField] private float milestoneSfxVolumeMultiplier = 1f;

        Image[] _digitSlots;
        readonly HashSet<int> _milestonesFired = new HashSet<int>();
        float _lastRefreshUnscaledTime = -999f;
        bool _wasGliding;
        Sequence _pulseSeq;
        int _lastDisplayedMeter = -1;

        void Awake()
        {
            if (glide == null)
                glide = UnityObjectFinder.FindFirst<GlideController>();
            if (stats == null)
                stats = UnityObjectFinder.FindFirst<StatsAggregator>();

            if (digitSprites != null && digitSprites.Length < 10)
                Debug.LogWarning($"{nameof(GlideFlightDistanceHud)}: digitSprites 需要至少 10 张（0～9）。");

            EnsureDigitStrip();
            if (pulseTarget == null && digitStrip != null)
                pulseTarget = digitStrip;

            if (hudRoot != null)
                hudRoot.SetActive(false);
        }

        void OnDestroy()
        {
            _pulseSeq?.Kill();
            if (pulseTarget != null)
                pulseTarget.DOKill();
        }

        void LateUpdate()
        {
            if (glide == null)
                glide = UnityObjectFinder.FindFirst<GlideController>();

            var gliding = glide != null && glide.isGliding;

            if (!gliding)
            {
                if (_wasGliding)
                {
                    _milestonesFired.Clear();
                    _lastDisplayedMeter = -1;
                    ResetPulseTargetScale();
                }

                _wasGliding = false;
                if (hudRoot != null) hudRoot.SetActive(false);
                return;
            }

            _wasGliding = true;
            if (hudRoot != null && !hudRoot.activeSelf)
                hudRoot.SetActive(true);

            var now = Time.unscaledTime;
            if (now - _lastRefreshUnscaledTime < minRefreshIntervalSeconds)
                return;
            _lastRefreshUnscaledTime = now;

            var dist = Mathf.Max(0f, glide.CurrentGlideDistanceFromTowerMeters);
            var meter = Mathf.FloorToInt(dist);
            if (meter != _lastDisplayedMeter)
            {
                _lastDisplayedMeter = meter;
                ApplyMilestones(meter);
            }

            BindDigitStrip(meter);
            BindCoinPreview(glide);
        }

        void EnsureDigitStrip()
        {
            if (digitStrip == null && hudRoot != null)
            {
                var t = hudRoot.transform.Find("DigitStrip");
                if (t != null)
                    digitStrip = t as RectTransform;
            }

            if (digitStrip == null && hudRoot != null && autoDigitSlotCount > 0)
                digitStrip = BuildAutoDigitStrip();

            CacheDigitSlots();
        }

        RectTransform BuildAutoDigitStrip()
        {
            var go = new GameObject("DigitStrip", typeof(RectTransform));
            go.transform.SetParent(hudRoot.transform, false);
            var strip = go.GetComponent<RectTransform>();
            strip.anchorMin = new Vector2(0.5f, 1f);
            strip.anchorMax = new Vector2(0.5f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.anchoredPosition = new Vector2(0f, -8f);
            strip.sizeDelta = new Vector2(
                autoDigitCellSize.x * autoDigitSlotCount + autoDigitSpacing * (autoDigitSlotCount - 1),
                autoDigitCellSize.y);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = autoDigitSpacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(0, 0, 0, 0);

            var n = Mathf.Clamp(autoDigitSlotCount, 1, 32);
            for (var i = 0; i < n; i++)
            {
                var cell = new GameObject($"Digit{i}", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
                cell.transform.SetParent(strip, false);
                var le = cell.GetComponent<LayoutElement>();
                le.preferredWidth = autoDigitCellSize.x;
                le.preferredHeight = autoDigitCellSize.y;
                le.minWidth = autoDigitCellSize.x;
                le.minHeight = autoDigitCellSize.y;
                var img = cell.GetComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                cell.SetActive(false);
            }

            return strip;
        }

        void CacheDigitSlots()
        {
            if (digitStrip == null)
            {
                _digitSlots = System.Array.Empty<Image>();
                return;
            }

            var n = digitStrip.childCount;
            _digitSlots = new Image[n];
            for (var i = 0; i < n; i++)
                _digitSlots[i] = digitStrip.GetChild(i).GetComponent<Image>();
        }

        void BindDigitStrip(int meter)
        {
            if (_digitSlots == null || _digitSlots.Length == 0 || digitSprites == null || digitSprites.Length < 10)
                return;

            var s = Mathf.Max(0, meter).ToString();
            if (s.Length > _digitSlots.Length && _digitSlots.Length > 0)
                s = s.Substring(s.Length - _digitSlots.Length);
            var len = s.Length;
            for (var i = 0; i < _digitSlots.Length; i++)
            {
                var img = _digitSlots[i];
                if (img == null)
                    continue;

                if (i >= len)
                {
                    img.gameObject.SetActive(false);
                    continue;
                }

                var c = s[i] - '0';
                if (c < 0 || c > 9 || digitSprites[c] == null)
                {
                    img.gameObject.SetActive(false);
                    continue;
                }

                img.sprite = digitSprites[c];
                img.gameObject.SetActive(true);
            }
        }

        void ApplyMilestones(int meterFloor)
        {
            if (milestoneDistancesMeters == null || milestoneDistancesMeters.Length == 0)
                return;

            var newCount = 0;
            for (var i = 0; i < milestoneDistancesMeters.Length; i++)
            {
                var m = Mathf.FloorToInt(milestoneDistancesMeters[i]);
                if (m < 0) continue;
                if (meterFloor < m) continue;
                if (_milestonesFired.Add(m))
                    newCount++;
            }

            if (newCount > 0)
                PlayMilestonePulses(newCount);
        }

        void ResetPulseTargetScale()
        {
            var rt = pulseTarget != null ? pulseTarget : digitStrip;
            if (rt == null)
                return;
            rt.DOKill();
            rt.localScale = Vector3.one;
        }

        void PlayMilestonePulses(int newMilestoneCount)
        {
            if (pulseTarget == null)
                return;
            if (newMilestoneCount <= 0)
                return;

            _pulseSeq?.Kill();
            pulseTarget.DOKill();

            var rt = pulseTarget;
            _pulseSeq = DOTween.Sequence();
            _pulseSeq.SetUpdate(true);

            for (var i = 0; i < newMilestoneCount; i++)
                _pulseSeq.Append(BuildSingleMilestonePulse(rt));
        }

        /// <summary>在调用时刻读取 <paramref name="rt"/> 的缩放作为本段基准：基准→基准×峰值倍率→基准×常驻倍率。</summary>
        Tween BuildSingleMilestonePulse(RectTransform rt)
        {
            var basis = Mathf.Max(1e-4f, rt.localScale.x);
            var peak = basis * milestonePulsePeakMultiplier;
            var settle = basis * milestonePulseSettleMultiplier;
            var t1 = pulseDuration * 0.45f;
            var t2 = pulseDuration * 0.55f;

            var inner = DOTween.Sequence();
            inner.SetUpdate(true);
            if (playMilestoneSound && milestoneSfxVolumeMultiplier > 0f)
                inner.AppendCallback(() => AudioGameplayFeedback.PlayGlideDistanceMilestone(milestoneSfxVolumeMultiplier));
            inner.Append(rt.DOScale(peak, t1).SetEase(Ease.OutQuad));
            inner.Join(rt.DOPunchAnchorPos(Vector2.right * punchVibrationStrength, pulseDuration, punchVibrato, 0.5f));
            inner.Append(rt.DOScale(settle, t2).SetEase(Ease.InOutQuad));
            return inner;
        }

        void BindCoinPreview(GlideController g)
        {
            if (coinRewardText == null)
                return;

            var add = 0f;
            var mul = 0f;
            if (stats != null)
            {
                stats.Recompute();
                add = stats.Current.totalCoinsAdd;
                mul = stats.Current.totalCoinsMul;
            }

            var h = g.GlideSessionTowerHeightMeters;
            var dist = Mathf.Max(0f, g.CurrentGlideDistanceFromTowerMeters);

            var log = RewardCalculator.LogGlideCoinSettlement;
            RewardCalculator.LogGlideCoinSettlement = false;
            long coins;
            try
            {
                coins = RewardCalculator.CalculateGlideSettlement(h, dist, add, mul).coins;
            }
            finally
            {
                RewardCalculator.LogGlideCoinSettlement = log;
            }

            coinRewardText.text = string.Format(coinRewardFormat, coins);
        }
    }
}
