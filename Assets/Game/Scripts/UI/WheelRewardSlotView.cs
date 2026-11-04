using UnityEngine;
using UnityEngine.UI;
using Game.DailySignIn;

namespace Game.UI
{
    /// <summary>转盘单个扇区的图标 + 文案（挂在每一格的父物体上或实例化）。</summary>
    public sealed class WheelRewardSlotView : MonoBehaviour
    {
        public Image iconImage;
        public Text labelText;

        public void Bind(in RewardDef reward, Sprite iconOverride, string labelOverride)
        {
            var spr = iconOverride != null ? iconOverride : RewardPresentation.TryResolveIcon(reward);
            if (iconImage != null)
            {
                iconImage.sprite = spr;
                iconImage.enabled = spr != null;
            }

            if (labelText != null)
                labelText.text = !string.IsNullOrEmpty(labelOverride)
                    ? labelOverride
                    : RewardPresentation.FormatRewardLine(reward);
        }
    }
}
