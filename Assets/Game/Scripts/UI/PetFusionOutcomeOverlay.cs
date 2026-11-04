using System.Collections.Generic;
using Game.Collectibles;
using Game.Collectibles.Pets;
using Game.Collectibles.Stats;
using Game.Core.Config;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 宠物合成结算：单次成功 / 一键合成批量收获。Mask 与继续按钮均可关闭。
    /// </summary>
    public sealed class PetFusionOutcomeOverlay : MonoBehaviour
    {
        [Header("数据")]
        [SerializeField] private QualityVisualLibrary qualityVisualLibrary;

        [Header("单次合成成功页 — 引用场景/预制体组件")]
        [SerializeField] private GameObject singleViewRoot;
        [SerializeField] private Text singleTitleText;
        [SerializeField] private Text singleNameText;
        [SerializeField] private Text singleLevelText;
        [SerializeField] private Text singleQualityText;
        [SerializeField] private Text singleModifiersText;
        [SerializeField] private Text singleHintText;
        [SerializeField] private Image singlePetIcon;
        [SerializeField] private Image singleQualityFrame;
        [Tooltip("全屏半透明蒙版按钮，点击关闭。")]
        [SerializeField] private Button singleMaskButton;
        [Tooltip("「继续」等关闭按钮。")]
        [SerializeField] private Button singleContinueButton;

        [Header("单次弹窗动效")]
        [Tooltip("单次结果缩放入场节点；留空则退化为整块 singleViewRoot 的 RectTransform。")]
        [SerializeField] private RectTransform singleIntroAnimRoot;
        [SerializeField] private bool singleIntroPopEnabled = true;
        [SerializeField] private float singleIntroInDuration = 0.22f;
        [SerializeField] private float singleIntroSettleDuration = 0.28f;
        [SerializeField] private float singleIntroPopScale = 1.1f;
        [SerializeField] private float singleIntroStartScale = 0.08f;
        [SerializeField] private bool singlePetIconWobbleEnabled = true;
        [SerializeField] private float singlePetWobbleAngle = 6.5f;
        [SerializeField] private float singlePetWobbleHalfPeriod = 0.42f;

        [Header("一键合成批量页 — 引用场景/预制体组件")]
        [SerializeField] private GameObject batchViewRoot;
        [SerializeField] private Text batchTitleText;
        [SerializeField] private Text batchHintText;
        [Tooltip("列表行父节点（Grid/VerticalLayout 的 Content，或 ScrollRect 的 Content）。")]
        [SerializeField] private RectTransform batchListContent;
        [SerializeField] private ScrollRect batchScrollRect;
        [Tooltip("批量列表行预制体（须含 PetFusionBatchRowView），必选。")]
        [SerializeField] private PetFusionBatchRowView batchRowPrefab;
        [SerializeField] private Button batchMaskButton;
        [SerializeField] private Button batchContinueButton;

        [Header("一键合成弹窗动效")]
        [Tooltip("批量页缩放入场节点；留空则退化为 batchViewRoot / 其子节点 Panel。")]
        [SerializeField] private RectTransform batchIntroAnimRoot;
        [SerializeField] private bool batchIntroPopEnabled = true;
        [SerializeField] private float batchIntroInDuration = 0.22f;
        [SerializeField] private float batchIntroSettleDuration = 0.28f;
        [SerializeField] private float batchIntroPopScale = 1.1f;
        [SerializeField] private float batchIntroStartScale = 0.08f;

        GameObject _singleRoot;
        GameObject _batchRoot;
        Text _singleTitle;
        Text _singleName;
        Text _singleLevel;
        Text _singleQuality;
        Text _singleMods;
        Text _singleHint;
        Image _singleIcon;
        Image _singleFrame;

        Text _batchTitle;
        Text _batchHint;
        RectTransform _batchContent;
        ScrollRect _batchScroll;

        bool _built;
        bool _singleMythicActive;
        string _lastSinglePetId;

        Sequence _singleIntroSeq;
        Sequence _batchIntroSeq;

        QualityVisualLibrary Lib => qualityVisualLibrary != null ? qualityVisualLibrary : ConfigRegistry.QualityVisuals;

        static CosmeticQuality Qual(PetQuality q) =>
            (CosmeticQuality)Mathf.Clamp((int)q, 0, (int)CosmeticQuality.Mythic);

        void Awake() => EnsureBuilt();

        void OnDestroy() => StopAllViewEffects(resetTransforms: true);

        public void SetQualityLibrary(QualityVisualLibrary lib) => qualityVisualLibrary = lib;

        /// <summary>将 Inspector 引用同步到内部逻辑；并给蒙版/继续按钮绑定关闭。</summary>
        void ResolveBindingsFromSerialized()
        {
            if (singleViewRoot != null) _singleRoot = singleViewRoot;
            if (singleTitleText != null) _singleTitle = singleTitleText;
            if (singleNameText != null) _singleName = singleNameText;
            if (singleLevelText != null) _singleLevel = singleLevelText;
            if (singleQualityText != null) _singleQuality = singleQualityText;
            if (singleModifiersText != null) _singleMods = singleModifiersText;
            if (singleHintText != null) _singleHint = singleHintText;
            if (singlePetIcon != null) _singleIcon = singlePetIcon;
            if (singleQualityFrame != null) _singleFrame = singleQualityFrame;
            WireCloseButton(singleMaskButton);
            WireCloseButton(singleContinueButton);

            if (batchViewRoot != null) _batchRoot = batchViewRoot;
            if (batchTitleText != null) _batchTitle = batchTitleText;
            if (batchHintText != null) _batchHint = batchHintText;
            if (batchListContent != null) _batchContent = batchListContent;
            if (batchScrollRect != null) _batchScroll = batchScrollRect;
            WireCloseButton(batchMaskButton);
            WireCloseButton(batchContinueButton);
        }

        void WireCloseButton(Button btn)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(HideAll);
        }

        public void ShowSingleSuccess(string petId)
        {
            EnsureBuilt();
            if (_singleRoot == null)
            {
                Debug.LogError($"{nameof(PetFusionOutcomeOverlay)}: 请绑定 {nameof(singleViewRoot)}。", this);
                return;
            }
            if (!ConfigRegistry.Pets.TryGet(petId, out var def) || def == null) return;

            StopBatchViewEffects(resetTransforms: true);

            _lastSinglePetId = petId;
            _batchRoot?.SetActive(false);
            _singleRoot?.SetActive(true);

            if (_singleTitle != null) _singleTitle.text = "合成成功!";
            if (_singleName != null) _singleName.text = def.displayName;
            if (_singleLevel != null) _singleLevel.text = $"等级: {def.level}级";
            if (_singleMods != null)
                _singleMods.text = StatModifiersUiFormat.FormatItemLine(def.modifiers);

            _singleMythicActive = Qual(def.quality) == CosmeticQuality.Mythic;
            ApplySingleQualityLine();

            if (_singleIcon != null)
            {
                if (def.inventoryIcon != null)
                {
                    _singleIcon.sprite = def.inventoryIcon;
                    _singleIcon.enabled = true;
                    _singleIcon.color = Color.white;
                }
                else
                {
                    _singleIcon.sprite = null;
                    _singleIcon.enabled = false;
                }

                _singleIcon.preserveAspect = true;
            }

            if (_singleFrame != null)
            {
                var lib = Lib;
                var sp = lib != null ? lib.ResolveSlotBackground(Qual(def.quality)) : null;
                _singleFrame.sprite = sp;
                _singleFrame.enabled = sp != null;
                _singleFrame.color = Color.white;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            PlaySingleViewEffects();
        }

        void ApplySingleQualityLine()
        {
            if (_singleQuality == null || string.IsNullOrWhiteSpace(_lastSinglePetId)) return;
            if (!ConfigRegistry.Pets.TryGet(_lastSinglePetId, out var def) || def == null)
            {
                _singleQuality.supportRichText = true;
                _singleQuality.text = "";
                return;
            }

            var lib = Lib;
            var cq = Qual(def.quality);
            _singleQuality.supportRichText = true;
            _singleQuality.text = QualityVisualLibrary.FormatQualityLineRichTextLoose(lib, cq, Time.unscaledTime);
        }

        public void ShowBatchRewards(IReadOnlyList<PetFusionService.FusionRewardEntry> rewards)
        {
            EnsureBuilt();
            if (rewards == null || rewards.Count == 0) return;

            if (_batchRoot == null)
            {
                Debug.LogError($"{nameof(PetFusionOutcomeOverlay)}: 请绑定 {nameof(batchViewRoot)}。", this);
                return;
            }

            if (batchRowPrefab == null)
            {
                Debug.LogError($"{nameof(PetFusionOutcomeOverlay)}: 请绑定 {nameof(batchRowPrefab)}。", this);
                return;
            }

            if (_batchContent == null)
            {
                Debug.LogError($"{nameof(PetFusionOutcomeOverlay)}: 请绑定 {nameof(batchListContent)}。", this);
                return;
            }

            StopSingleViewEffects(resetTransforms: true);
            StopBatchViewEffects(resetTransforms: false);

            _singleRoot?.SetActive(false);
            _batchRoot.SetActive(true);
            _lastSinglePetId = null;
            _singleMythicActive = false;

            if (_batchTitle != null) _batchTitle.text = "一键合成收获";

            for (var i = _batchContent.childCount - 1; i >= 0; i--)
                Destroy(_batchContent.GetChild(i).gameObject);

            var lib = Lib;
            foreach (var e in rewards)
            {
                var row = Instantiate(batchRowPrefab, _batchContent);
                row.Apply(e.PetId, e.Count, lib);
            }

            if (_batchScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _batchScroll.verticalNormalizedPosition = 1f;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            PlayBatchViewEffects();
        }

        void HideAll()
        {
            StopAllViewEffects(resetTransforms: true);
            _singleMythicActive = false;
            _lastSinglePetId = null;
            gameObject.SetActive(false);
        }

        void StopAllViewEffects(bool resetTransforms)
        {
            StopSingleViewEffects(resetTransforms);
            StopBatchViewEffects(resetTransforms);
        }

        RectTransform ResolveSingleIntroTarget()
        {
            if (singleIntroAnimRoot != null)
                return singleIntroAnimRoot;
            if (_singleRoot == null)
                return null;
            var panel = _singleRoot.transform.Find("Panel");
            if (panel != null)
                return panel as RectTransform;
            return _singleRoot.GetComponent<RectTransform>();
        }

        RectTransform ResolveBatchIntroTarget()
        {
            if (batchIntroAnimRoot != null)
                return batchIntroAnimRoot;
            if (_batchRoot == null)
                return null;
            var panel = _batchRoot.transform.Find("Panel");
            if (panel != null)
                return panel as RectTransform;
            return _batchRoot.GetComponent<RectTransform>();
        }

        void StopSingleViewEffects(bool resetTransforms)
        {
            _singleIntroSeq?.Kill();
            _singleIntroSeq = null;

            if (_singleIcon != null)
            {
                _singleIcon.transform.DOKill();
                if (resetTransforms)
                    _singleIcon.transform.localRotation = Quaternion.identity;
            }

            if (resetTransforms)
            {
                var introRt = ResolveSingleIntroTarget();
                if (introRt != null)
                {
                    introRt.DOKill();
                    introRt.localScale = Vector3.one;
                }
            }
        }

        void StopBatchViewEffects(bool resetTransforms)
        {
            _batchIntroSeq?.Kill();
            _batchIntroSeq = null;

            if (!resetTransforms)
                return;

            var introRt = ResolveBatchIntroTarget();
            if (introRt != null)
            {
                introRt.DOKill();
                introRt.localScale = Vector3.one;
            }
        }

        static void PlayPanelIntroPop(ref Sequence seqSlot, RectTransform introRt, bool popEnabled,
            float inDuration, float settleDuration, float peakScale, float startScale)
        {
            seqSlot?.Kill();
            seqSlot = null;

            if (introRt == null)
                return;

            introRt.DOKill();

            if (!popEnabled)
            {
                introRt.localScale = Vector3.one;
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
        }

        void PlaySingleViewEffects()
        {
            StopSingleViewEffects(resetTransforms: false);

            PlayPanelIntroPop(ref _singleIntroSeq, ResolveSingleIntroTarget(), singleIntroPopEnabled,
                singleIntroInDuration, singleIntroSettleDuration, singleIntroPopScale, singleIntroStartScale);

            if (singlePetIconWobbleEnabled && _singleIcon != null && singlePetWobbleHalfPeriod > 0.02f)
            {
                var ang = Mathf.Clamp(singlePetWobbleAngle, 0.5f, 25f);
                _singleIcon.transform.localRotation = Quaternion.identity;
                _singleIcon.transform
                    .DOLocalRotate(new Vector3(0f, 0f, ang), singlePetWobbleHalfPeriod, RotateMode.Fast)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }

        void PlayBatchViewEffects()
        {
            StopBatchViewEffects(resetTransforms: false);

            PlayPanelIntroPop(ref _batchIntroSeq, ResolveBatchIntroTarget(), batchIntroPopEnabled,
                batchIntroInDuration, batchIntroSettleDuration, batchIntroPopScale, batchIntroStartScale);
        }

        void Update()
        {
            if (!_singleMythicActive || _singleRoot == null || !_singleRoot.activeSelf) return;
            ApplySingleQualityLine();
        }

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            ResolveBindingsFromSerialized();

            if (_singleRoot != null) _singleRoot.SetActive(false);
            if (_batchRoot != null) _batchRoot.SetActive(false);

            gameObject.SetActive(false);
        }
    }
}
