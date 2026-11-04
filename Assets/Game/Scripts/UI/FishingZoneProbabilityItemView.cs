using Game.Collectibles;
using Game.Core.Config;
using Game.Fishing.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 钓鱼区概率公示单格：品质底框 + 图标 + 概率 + 末位鱼闪光。
    /// </summary>
    public sealed class FishingZoneProbabilityItemView : MonoBehaviour
    {
        static readonly Color LockedIconColor = Color.black;

        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text possibilityText;
        [SerializeField] private GameObject sparkleRoot;
        [SerializeField] private QualityVisualLibrary qualityVisualLibrary;

        CosmeticQuality _quality = CosmeticQuality.Common;
        bool _mythicQuality;
        bool _useMythicRainbowText;
        QualityVisualLibrary _library;

        void Reset() => ResolveReferences();

        void Awake() => ResolveReferences();

        void Update()
        {
            if (!_useMythicRainbowText || possibilityText == null || _library == null)
                return;

            possibilityText.color = _library.ResolveMythicRainbowColor(Time.unscaledTime);
        }

        public void Apply(
            FishDef fish,
            float probability01,
            bool isDiscovered,
            bool isLastInZone,
            QualityVisualLibrary library)
        {
            ResolveReferences();
            gameObject.SetActive(true);
            _library = library != null ? library : ConfigRegistry.QualityVisuals;
            _quality = fish != null ? fish.quality : CosmeticQuality.Common;
            _mythicQuality = _quality == CosmeticQuality.Mythic;
            _useMythicRainbowText = isDiscovered && _mythicQuality;

            var nameNode = transform.Find("Icon/Name");
            if (nameNode != null)
                nameNode.gameObject.SetActive(false);

            if (possibilityText != null)
            {
                possibilityText.text = $"{probability01 * 100f:0.#}%";
                possibilityText.color = isDiscovered && _library != null
                    ? (_mythicQuality
                        ? _library.ResolveMythicRainbowColor(Time.unscaledTime)
                        : _library.ResolveRichTextColor(_quality))
                    : Color.white;
            }

            var slotBg = _library != null ? _library.ResolveSlotBackground(_quality) : null;
            if (frameImage != null)
            {
                if (slotBg != null)
                {
                    frameImage.sprite = slotBg;
                    frameImage.enabled = true;
                    frameImage.color = Color.white;
                }
                else
                {
                    frameImage.sprite = null;
                    frameImage.enabled = false;
                }
            }

            if (iconImage != null)
            {
                var icon = fish != null ? fish.icon : null;
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.preserveAspect = true;
                iconImage.color = isDiscovered ? Color.white : LockedIconColor;
            }

            if (sparkleRoot != null)
                sparkleRoot.SetActive(isLastInZone && isDiscovered);
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        void ResolveReferences()
        {
            if (frameImage == null)
            {
                frameImage = transform.Find("Frame")?.GetComponent<Image>()
                             ?? GetComponent<Image>();
            }

            if (iconImage == null)
            {
                var iconTransform = transform.Find("Icon");
                iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            }

            if (possibilityText == null)
            {
                var iconTransform = transform.Find("Icon");
                if (iconTransform != null)
                    possibilityText = iconTransform.Find("Possibility")?.GetComponent<Text>();
            }

            if (sparkleRoot == null)
            {
                sparkleRoot = transform.Find("Sparkle")?.gameObject
                              ?? transform.Find("Shine")?.gameObject
                              ?? transform.Find("Effect")?.gameObject
                              ?? transform.Find("闪光")?.gameObject;
            }
        }
    }
}
