using Game.Collectibles;
using Game.Collectibles.Pets;
using Game.Core.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 通用宠物格子：品质框、图标、名称、第四格文本（一键合成用作数量×n；孵化结果可隐藏；
    /// 概率列表用作右上角百分比）。布局由预制体/场景负责，不在此运行时生成。
    /// </summary>
    public sealed class PetFusionBatchRowView : MonoBehaviour
    {
        [SerializeField] private Image qualityFrame;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text countText;

        static CosmeticQuality Qual(PetQuality q) =>
            (CosmeticQuality)Mathf.Clamp((int)q, 0, (int)CosmeticQuality.Mythic);

        public void Apply(string petId, int count, QualityVisualLibrary lib)
        {
            ApplyPetData(petId, lib);
            if (countText != null)
            {
                countText.gameObject.SetActive(true);
                countText.text = $"×{count}";
            }
        }

        /// <summary>孵化结果等：只显示图标、框、名称，不显示数量。</summary>
        public void ApplyDisplayOnly(string petId, QualityVisualLibrary lib)
        {
            ApplyPetData(petId, lib);
            if (countText != null)
                countText.gameObject.SetActive(false);
        }

        /// <summary>概率列表：<paramref name="rightLabel"/> 显示在数量文本槽位（例如 12.5%）。</summary>
        public void ApplyWithRightTopLabel(string petId, QualityVisualLibrary lib, string rightLabel)
        {
            ApplyPetData(petId, lib);
            if (countText == null) return;
            countText.gameObject.SetActive(!string.IsNullOrEmpty(rightLabel));
            countText.text = rightLabel ?? "";
        }

        public void ApplyProbabilityPercent(string petId, QualityVisualLibrary lib, float probability01)
        {
            var s = $"{probability01 * 100f:0.#}%";
            ApplyWithRightTopLabel(petId, lib, s);
        }

        void ApplyPetData(string petId, QualityVisualLibrary lib)
        {
            PetDef def = null;
            if (ConfigRegistry.Pets != null)
                ConfigRegistry.Pets.TryGet(petId, out def);

            if (nameText != null)
                nameText.text = def != null ? def.displayName : petId;

            if (iconImage != null)
            {
                if (def != null && def.inventoryIcon != null)
                {
                    iconImage.sprite = def.inventoryIcon;
                    iconImage.enabled = true;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.enabled = false;
                }

                iconImage.preserveAspect = true;
            }

            if (qualityFrame != null)
            {
                if (def != null && lib != null)
                {
                    var sp = lib.ResolveSlotBackground(Qual(def.quality));
                    qualityFrame.sprite = sp;
                    qualityFrame.enabled = sp != null;
                    qualityFrame.color = Color.white;
                }
                else
                {
                    qualityFrame.sprite = null;
                    qualityFrame.enabled = false;
                }
            }
        }
    }
}
