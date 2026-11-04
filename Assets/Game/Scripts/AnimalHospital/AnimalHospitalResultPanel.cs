using Game.Reward;
using Game.Audio;
using Game.Maps;
using Game.Save;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.AnimalHospital
{
    /// <summary>换班结算：展示本日数据，结算/双倍结算后进入下一天。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalResultPanel : AnimalHospitalSlidingPanelBase
    {
        [Header("Texts")]
        [SerializeField] private Text survivalDayText;
        [SerializeField] private Text plannedPatientsText;
        [SerializeField] private Text treatedPatientsText;
        [SerializeField] private Text anomalousPhotographedText;
        [SerializeField] private Text anomalousHandledText;
        [SerializeField] private Text performanceText;
        [SerializeField] private Text coinsEarnedText;
        [SerializeField] private Text hintText;

        [Header("Buttons")]
        [SerializeField] private Button settleButton;
        [SerializeField] private Button doubleSettleButton;
        [SerializeField] private Button homeButton;

        AnimalHospitalResultViewModel _model;
        bool _settled;

        public override UiPanelId PanelId => UiPanelId.AnimalHospitalResult;

        protected override void Awake()
        {
            allowMaskClose = false;
            settleButton?.onClick.AddListener(OnSettle);
            doubleSettleButton?.onClick.AddListener(OnDoubleSettle);
            homeButton?.onClick.AddListener(OnHome);
            base.Awake();
        }

        protected override void OnDestroy()
        {
            settleButton?.onClick.RemoveListener(OnSettle);
            doubleSettleButton?.onClick.RemoveListener(OnDoubleSettle);
            homeButton?.onClick.RemoveListener(OnHome);
            base.OnDestroy();
        }

        public override void Open(UiOpenPayload payload)
        {
            _settled = false;
            _model = payload?.AnimalHospitalResult;
            if (_model == null)
            {
                Debug.LogWarning($"{nameof(AnimalHospitalResultPanel)}: 缺少结算数据。");
                return;
            }

            BindModel(_model);
            Show();
        }

        public override void Close() => Hide(() => _model = null);

        void BindModel(AnimalHospitalResultViewModel model)
        {
            SetText(survivalDayText, $"{model.SurvivalDay}天");
            SetText(plannedPatientsText, model.PlannedPatientCount.ToString());
            SetText(treatedPatientsText, model.TreatedCount.ToString());
            SetText(anomalousPhotographedText, model.AnomalousPhotographedCount.ToString());
            SetText(anomalousHandledText, model.AnomalousHandledCount.ToString());
            SetText(performanceText, model.PerformanceGrade ?? "普通");
            SetText(coinsEarnedText, model.CoinsEarned.ToString());
            SetText(hintText, string.Empty);
        }

        static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value ?? string.Empty;
        }

        void OnSettle() => TrySettle(doubleReward: false);

        void OnDoubleSettle()
        {
            if (_settled)
                return;

            if (_model == null || _model.TreatedCount < 1)
            {
                ShowNeedWorkHint();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            FreeRewardGate.Grant(() => TrySettle(doubleReward: true));
        }

        void TrySettle(bool doubleReward)
        {
            if (_settled)
                return;

            if (_model == null || _model.TreatedCount < 1)
            {
                ShowNeedWorkHint();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            _settled = true;
            var coins = Mathf.Max(0, _model.CoinsEarned);
            if (doubleReward)
                coins *= 2;

            if (coins > 0)
                ModeSaveAccess.AddCoins(coins);

            var save = SaveService.Data?.animalHospital;
            if (save != null)
            {
                save.currentDay = Mathf.Max(1, save.currentDay) + 1;
                SaveService.Save();
            }

            HideImmediate();
            _model = null;

            var director = AnimalHospitalDirector.Instance ?? FindObjectOfType<AnimalHospitalDirector>();
            director?.ContinueNextDay();
        }

        void ShowNeedWorkHint()
        {
            const string msg = "请开始今日的工作。";
            SetText(hintText, msg);
            var director = AnimalHospitalDirector.Instance ?? FindObjectOfType<AnimalHospitalDirector>();
            director?.Service?.ShowToast(msg);
        }

        void OnHome()
        {
            AudioGameplayFeedback.PlayUiButton();
            HideImmediate();
            _model = null;

            var director = AnimalHospitalDirector.Instance ?? FindObjectOfType<AnimalHospitalDirector>();
            director?.Service?.StopDay();
            GameSceneLoader.NavigateToMainHome();
        }
    }
}
