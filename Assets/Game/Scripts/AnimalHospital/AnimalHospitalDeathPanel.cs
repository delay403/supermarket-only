using Game.Reward;
using Game.Audio;
using Game.Save;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.AnimalHospital
{
    /// <summary>理智归零：重新开始或恢复理智。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalDeathPanel : AnimalHospitalSlidingPanelBase
    {
        [Header("Content")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button recoverButton;

        public override UiPanelId PanelId => UiPanelId.AnimalHospitalDeath;

        protected override void Awake()
        {
            allowMaskClose = false;
            restartButton?.onClick.AddListener(OnRestart);
            recoverButton?.onClick.AddListener(OnRecover);
            ApplyDefaultCopy();
            base.Awake();
        }

        protected override void OnDestroy()
        {
            restartButton?.onClick.RemoveListener(OnRestart);
            recoverButton?.onClick.RemoveListener(OnRecover);
            base.OnDestroy();
        }

        public override void Open(UiOpenPayload payload)
        {
            ApplyDefaultCopy();
            Show();
        }

        public override void Close() => Hide();

        void ApplyDefaultCopy()
        {
            if (titleText != null)
                titleText.text = "彻底失智";
            if (descriptionText != null)
                descriptionText.text = "理智值为0，彻底失智！是否立即恢复理智";
        }

        void OnRestart()
        {
            AudioGameplayFeedback.PlayUiButton();
            HideImmediate();

            var save = SaveService.Data?.animalHospital;
            if (save != null)
            {
                save.currentDay = 1;
                SaveService.Save();
            }

            var director = AnimalHospitalDirector.Instance ?? FindObjectOfType<AnimalHospitalDirector>();
            director?.RestartAfterDeath();
        }

        void OnRecover()
        {
            AudioGameplayFeedback.PlayUiButton();
            FreeRewardGate.Grant(() =>
            {
                HideImmediate();
                var director = AnimalHospitalDirector.Instance ?? FindObjectOfType<AnimalHospitalDirector>();
                director?.RecoverSanityFromDeathOffer();
            });
        }
    }
}
