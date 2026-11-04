using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    [DisallowMultipleComponent]
    public sealed class SupermarketCrosshairUi : MonoBehaviour
    {
        [SerializeField] private Image crosshairImage;
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField] private Color crosshairActiveColor = new Color(1f, 0.92f, 0.2f, 1f);
        [SerializeField] private float crosshairActiveSizeMultiplier = 1.35f;
        [SerializeField] private Vector2 crosshairDefaultSize = new Vector2(14f, 14f);

        RectTransform _crosshairRect;
        bool _defaultsCached;

        void Awake() => CacheDefaults(force: true);

        public void ApplyAimState(SupermarketAimState state)
        {
            CacheDefaults(force: false);
            if (crosshairImage == null || _crosshairRect == null)
                return;

            var active = state.canPickUp || state.canPlace;
            crosshairImage.color = active ? crosshairActiveColor : crosshairColor;
            _crosshairRect.localScale = Vector3.one;
            _crosshairRect.sizeDelta = active
                ? crosshairDefaultSize * crosshairActiveSizeMultiplier
                : crosshairDefaultSize;
        }

        public void EnsureDefaultCrosshair()
        {
            CacheDefaults(force: false);
            if (_crosshairRect == null)
                return;

            _crosshairRect.localScale = Vector3.one;
            _crosshairRect.sizeDelta = crosshairDefaultSize;
            if (crosshairImage != null)
                crosshairImage.color = crosshairColor;
        }

        void CacheDefaults(bool force)
        {
            if (_defaultsCached && !force)
                return;

            if (crosshairImage == null)
                crosshairImage = GetComponent<Image>();
            if (crosshairImage == null)
            {
                crosshairImage = gameObject.AddComponent<Image>();
                crosshairImage.raycastTarget = false;
            }

            _crosshairRect = crosshairImage.rectTransform;
            if (!_defaultsCached && _crosshairRect.sizeDelta.sqrMagnitude > 1f)
                crosshairDefaultSize = _crosshairRect.sizeDelta;

            _crosshairRect.localScale = Vector3.one;
            if (!_defaultsCached && crosshairImage != null)
                crosshairImage.color = crosshairColor;
            _defaultsCached = true;
        }
    }
}
