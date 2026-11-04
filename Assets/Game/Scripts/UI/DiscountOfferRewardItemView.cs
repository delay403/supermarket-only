using Game.Collectibles;
using Game.Core.Config;
using Game.Economy;
using Game.Economy.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class DiscountOfferRewardItemView : MonoBehaviour
    {
        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text labelText;
        [SerializeField] private Sprite fallbackCoinIcon;
        [SerializeField] private Sprite fallbackGemIcon;

        void Awake() => ResolveBindings();

        void ResolveBindings()
        {
            if (frameImage == null)
            {
                var frame = transform.Find("Frame");
                if (frame != null)
                    frameImage = frame.GetComponent<Image>();
            }

            if (iconImage == null)
            {
                var icon = transform.Find("Icon");
                if (icon != null)
                    iconImage = icon.GetComponent<Image>();
            }

            if (labelText == null)
            {
                var label = transform.Find("Label");
                if (label != null)
                    labelText = label.GetComponent<Text>();
            }
        }

        public void Bind(DiscountOfferRewardEntry reward)
        {
            ResolveBindings();
            var library = ConfigRegistry.QualityVisuals;

            if (labelText != null)
                labelText.text = DiscountOfferRewardPresenter.ResolveLabel(reward);

            if (frameImage != null)
            {
                var frame = DiscountOfferRewardPresenter.ResolveFrame(reward, library);
                if (frame != null)
                {
                    frameImage.enabled = true;
                    frameImage.sprite = frame;
                }
            }

            if (iconImage == null)
                return;

            var icon = DiscountOfferRewardPresenter.ResolveIcon(reward, fallbackCoinIcon, fallbackGemIcon);
            if (icon != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = icon;
            }
            else
            {
                iconImage.enabled = false;
            }
        }
    }
}
