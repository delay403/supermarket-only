using UnityEngine;
using UnityEngine.UI;

namespace Game.AnimalHospital
{
    /// <summary>屏幕中心准星：默认常显，瞄准可交互物体时高亮。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalCrosshairUi : MonoBehaviour
    {
        [SerializeField] private Image crosshairImage;
        [SerializeField] private Image horizontalBar;
        [SerializeField] private Image verticalBar;
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.75f);
        [SerializeField] private Color activeColor = new Color(1f, 0.92f, 0.2f, 1f);
        [SerializeField] private Color holdingColor = new Color(0.35f, 0.95f, 0.45f, 1f);
        [SerializeField] private float activeSizeMultiplier = 1.35f;
        [SerializeField] private Vector2 defaultSize = new Vector2(14f, 14f);

        RectTransform _crosshairRect;
        bool _defaultsCached;

        void Awake() => CacheDefaults(force: true);

        public void ApplyAimTarget(AnimalHospitalInteractable target, bool holding)
        {
            CacheDefaults(force: false);

            Color color;
            if (holding && target != null)
                color = holdingColor;
            else if (target != null)
                color = activeColor;
            else
                color = idleColor;

            ApplyColor(color);

            if (_crosshairRect != null)
            {
                _crosshairRect.localScale = Vector3.one;
                _crosshairRect.sizeDelta = target != null
                    ? defaultSize * activeSizeMultiplier
                    : defaultSize;
            }
        }

        public void EnsureDefaultCrosshair()
        {
            CacheDefaults(force: false);
            if (_crosshairRect != null)
            {
                _crosshairRect.localScale = Vector3.one;
                _crosshairRect.sizeDelta = defaultSize;
            }

            ApplyColor(idleColor);
        }

        void ApplyColor(Color color)
        {
            if (crosshairImage != null)
                crosshairImage.color = color;
            if (horizontalBar != null)
                horizontalBar.color = color;
            if (verticalBar != null)
                verticalBar.color = color;
        }

        void CacheDefaults(bool force)
        {
            if (_defaultsCached && !force)
                return;

            if (crosshairImage == null)
                crosshairImage = GetComponent<Image>();

            if (horizontalBar == null || verticalBar == null)
                ResolveCrossBars();

            if (crosshairImage == null && horizontalBar == null && verticalBar == null)
            {
                crosshairImage = gameObject.AddComponent<Image>();
                crosshairImage.raycastTarget = false;
            }

            _crosshairRect = crosshairImage != null
                ? crosshairImage.rectTransform
                : horizontalBar != null
                    ? horizontalBar.rectTransform.parent as RectTransform
                    : transform as RectTransform;

            if (!_defaultsCached && _crosshairRect != null && _crosshairRect.sizeDelta.sqrMagnitude > 1f)
                defaultSize = _crosshairRect.sizeDelta;

            if (_crosshairRect != null)
                _crosshairRect.localScale = Vector3.one;

            if (!_defaultsCached)
                ApplyColor(idleColor);

            _defaultsCached = true;
        }

        void ResolveCrossBars()
        {
            var bars = transform.Find("Bars");
            if (bars == null)
                return;

            if (horizontalBar == null)
            {
                var h = bars.Find("Horizontal");
                if (h != null)
                    horizontalBar = h.GetComponent<Image>();
            }

            if (verticalBar == null)
            {
                var v = bars.Find("Vertical");
                if (v != null)
                    verticalBar = v.GetComponent<Image>();
            }
        }

        public void InitializeBars(Image horizontal, Image vertical)
        {
            horizontalBar = horizontal;
            verticalBar = vertical;
            _defaultsCached = false;
            CacheDefaults(force: true);
        }

        public static AnimalHospitalCrosshairUi CreateCrosshair(Transform parent)
        {
            var root = new GameObject("Crosshair", typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(22f, 22f);

            var bars = new GameObject("Bars", typeof(RectTransform));
            bars.transform.SetParent(root.transform, false);
            var barsRt = bars.GetComponent<RectTransform>();
            barsRt.anchorMin = Vector2.zero;
            barsRt.anchorMax = Vector2.one;
            barsRt.offsetMin = Vector2.zero;
            barsRt.offsetMax = Vector2.zero;

            var horizontal = CreateBar(bars.transform, "Horizontal", new Vector2(18f, 3f));
            var vertical = CreateBar(bars.transform, "Vertical", new Vector2(3f, 18f));

            var ui = root.AddComponent<AnimalHospitalCrosshairUi>();
            ui.InitializeBars(horizontal, vertical);
            return ui;
        }

        static Image CreateBar(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0.75f);
            return image;
        }
    }
}
