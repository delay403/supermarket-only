using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Maps
{
    /// <summary>
    /// 挂在过场 Canvas 根节点（或任意持久节点）：引用场景里已摆放的 Mask / BG，由 <see cref="SceneTransitionOverlay"/> 驱动动画。
    /// Awake 时会对根物体执行 DontDestroyOnLoad，全项目只应存在一个实例。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransitionOverlayBinding : MonoBehaviour
    {
        public static TransitionOverlayBinding Instance { get; private set; }

        static Sprite _whiteSprite;

        [Tooltip("整层透明度与射线拦截；不填则在根节点上查找。")]
        [SerializeField] CanvasGroup rootCanvasGroup;

        [Tooltip("带 Mask 组件、需要做 scale 动画的节点（例如 Mask）。")]
        [SerializeField] RectTransform maskScaleRoot;

        [Tooltip("Mask 上的用于裁切形状的 Image；不填则在 maskScaleRoot 上查找。")]
        [SerializeField] Image maskGraphic;

        [Tooltip("纯色底图 BG（用于 transitionCoverColor）。")]
        [SerializeField] Image backgroundImage;

        bool _coversScreen;

        public static bool HasInstance => Instance != null;

        public static bool IsScreenCoveredForActivation => Instance != null && Instance._coversScreen;

        void Awake()
        {
            TryClaimSingleton();
        }

        /// <summary>若 Awake 未认领（例如后置加载场景），可在外部调用一次。</summary>
        public void TryClaimSingleton()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);

            if (rootCanvasGroup == null)
            {
                var root = transform.root;
                rootCanvasGroup = root.GetComponent<CanvasGroup>();
                if (rootCanvasGroup == null)
                    rootCanvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            }

            if (maskScaleRoot == null)
            {
                Debug.LogError($"{nameof(TransitionOverlayBinding)}: 未指定 {nameof(maskScaleRoot)}。", this);
                return;
            }

            if (maskGraphic == null)
                maskGraphic = maskScaleRoot.GetComponent<Image>();
            if (backgroundImage == null)
                Debug.LogWarning($"{nameof(TransitionOverlayBinding)}: 未指定 {nameof(backgroundImage)}，将无法应用底色。", this);

            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.blocksRaycasts = false;
            rootCanvasGroup.interactable = false;

            ApplyFromSettings(SceneNavigationSettings.ResolveActive());
            SnapMaskScale(SceneNavigationSettings.ResolveActive(), closed: false);

            _coversScreen = false;
            RefreshRaycastBlock();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public IEnumerator CoCover(SceneNavigationSettings cfg, Action<float> onProgress = null)
        {
            rootCanvasGroup.blocksRaycasts = true;
            rootCanvasGroup.interactable = true;

            if (cfg == null || maskScaleRoot == null || maskGraphic == null)
                yield break;

            ApplyPendingBackgroundIfAny();
            ApplyFromSettings(cfg);
            rootCanvasGroup.alpha = 1f;

            var minS = Mathf.Min(cfg.maskScaleMin, cfg.maskScaleMax);
            var maxS = Mathf.Max(cfg.maskScaleMin, cfg.maskScaleMax);
            maskScaleRoot.localScale = Vector3.one * minS;
            onProgress?.Invoke(0f);
            yield return AnimateMaskScale(minS, maxS, cfg.MaskCloseSeconds, onProgress);
            maskScaleRoot.localScale = Vector3.one * maxS;
            onProgress?.Invoke(1f);

            _coversScreen = true;
            RefreshRaycastBlock();
        }

        public IEnumerator CoReveal(SceneNavigationSettings cfg)
        {
            if (cfg == null || maskScaleRoot == null)
            {
                if (rootCanvasGroup != null)
                    rootCanvasGroup.alpha = 0f;
                _coversScreen = false;
                RefreshRaycastBlock();
                yield break;
            }

            ApplyFromSettings(cfg);
            rootCanvasGroup.alpha = 1f;

            var minS = Mathf.Min(cfg.maskScaleMin, cfg.maskScaleMax);
            var maxS = Mathf.Max(cfg.maskScaleMin, cfg.maskScaleMax);
            maskScaleRoot.localScale = Vector3.one * maxS;
            yield return AnimateMaskScale(maxS, minS, cfg.MaskOpenSeconds);
            maskScaleRoot.localScale = Vector3.one * minS;

            rootCanvasGroup.alpha = 0f;
            _coversScreen = false;
            RefreshRaycastBlock();
        }

        void ApplyFromSettings(SceneNavigationSettings cfg)
        {
            if (maskGraphic != null)
            {
                maskGraphic.sprite = cfg != null && cfg.maskShapeSprite != null ? cfg.maskShapeSprite : GetWhiteSprite();
                maskGraphic.type = Image.Type.Simple;
                maskGraphic.preserveAspect = false;
            }

            if (backgroundImage != null)
            {
                if (backgroundImage.sprite == null)
                {
                    var c = cfg != null ? cfg.transitionCoverColor : Color.black;
                    var a = c.a > 0f ? c.a : 1f;
                    backgroundImage.color = new Color(c.r, c.g, c.b, a);
                }
            }
        }

        void ApplyPendingBackgroundIfAny()
        {
            if (backgroundImage == null)
            {
                SceneTransitionPresentation.ConsumePendingBackground();
                return;
            }

            var pending = SceneTransitionPresentation.ConsumePendingBackground();
            if (pending == null)
            {
                backgroundImage.sprite = null;
                return;
            }

            backgroundImage.sprite = pending;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = true;
            backgroundImage.color = Color.white;
        }

        void SnapMaskScale(SceneNavigationSettings cfg, bool closed)
        {
            if (maskScaleRoot == null)
                return;

            var minS = cfg != null ? Mathf.Min(cfg.maskScaleMin, cfg.maskScaleMax) : 0f;
            var maxS = cfg != null ? Mathf.Max(cfg.maskScaleMin, cfg.maskScaleMax) : 20f;
            maskScaleRoot.localScale = Vector3.one * (closed ? maxS : minS);
        }

        IEnumerator AnimateMaskScale(float fromScale, float toScale, float durationSeconds, Action<float> onProgress = null)
        {
            durationSeconds = Mathf.Max(0f, durationSeconds);
            if (maskScaleRoot == null)
                yield break;

            if (durationSeconds <= 0f)
            {
                maskScaleRoot.localScale = Vector3.one * toScale;
                onProgress?.Invoke(1f);
                yield break;
            }

            var t = 0f;
            while (t < durationSeconds)
            {
                t += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(t / durationSeconds);
                var s = Mathf.Lerp(fromScale, toScale, k);
                maskScaleRoot.localScale = Vector3.one * s;
                onProgress?.Invoke(k);
                yield return null;
            }

            maskScaleRoot.localScale = Vector3.one * toScale;
            onProgress?.Invoke(1f);
        }

        void RefreshRaycastBlock()
        {
            if (rootCanvasGroup == null)
                return;
            rootCanvasGroup.blocksRaycasts = _coversScreen;
            rootCanvasGroup.interactable = _coversScreen;
        }

        static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;

            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _whiteSprite;
        }
    }
}
