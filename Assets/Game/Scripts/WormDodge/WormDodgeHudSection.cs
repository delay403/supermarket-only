using Game.Collectibles.Stats;
using Game.Core;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.WormDodge
{
    /// <summary>躲避虫子 HUD：当前层数、本局金币、追击提示。</summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeHudSection : MonoBehaviour
    {
        [SerializeField] private Text floorText;
        [SerializeField] private Text runCoinsText;
        [SerializeField] private Text chaseHintText;
        [SerializeField] private Text equipmentBonusText;

        void OnEnable()
        {
            ApplyProductModeVisibility();
            RefreshAll();
            WormDodgeDirector.RunStarted += OnRunStarted;
            BindDirectorInstanceEvents(true);
        }

        void OnDisable()
        {
            WormDodgeDirector.RunStarted -= OnRunStarted;
            BindDirectorInstanceEvents(false);
        }

        void OnDestroy()
        {
            WormDodgeDirector.RunStarted -= OnRunStarted;
            BindDirectorInstanceEvents(false);
        }

        static void OnRunStarted() => RefreshAllInScene();

        void BindDirectorInstanceEvents(bool bind)
        {
            var director = WormDodgeDirector.Instance;
            if (director == null)
                return;

            if (bind)
            {
                director.FloorReached += OnFloorReached;
                director.RunEnded += OnRunEnded;
                director.RunResumedFromRevive += OnRunResumedFromRevive;
            }
            else
            {
                director.FloorReached -= OnFloorReached;
                director.RunEnded -= OnRunEnded;
                director.RunResumedFromRevive -= OnRunResumedFromRevive;
            }
        }

        void OnRunResumedFromRevive() => RefreshAll();

        void OnFloorReached(int _, long __) => RefreshAll();

        void OnRunEnded(Game.UI.ViewModels.WormDodgeResultViewModel _) => RefreshAll();

        public static void RefreshAllInScene()
        {
#if UNITY_2023_1_OR_NEWER
            var sections = FindObjectsByType<WormDodgeHudSection>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var sections = FindObjectsOfType<WormDodgeHudSection>(true);
#endif
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                    continue;

                section.RefreshAll();
            }
        }

        public void RefreshAll()
        {
            if (this == null)
                return;

            var director = WormDodgeDirector.Instance;
            if (floorText != null)
                floorText.text = director != null ? $"当前层数：{director.HighestFloorThisRun}" : "当前层数：0";

            if (runCoinsText != null)
                runCoinsText.text = director != null ? $"本局 +{director.CoinsThisRun}" : "本局 +0";

            if (chaseHintText != null)
            {
                var motor = FindObjectOfType<EscapeSnake.WormChainMotor>();
                if (motor != null && motor.IsChasing && motor.ActiveChaseTarget != null)
                    chaseHintText.text = "虫子正在追击！";
                else
                    chaseHintText.text = string.Empty;
            }

            RefreshEquipmentBonusText();
        }

        void RefreshEquipmentBonusText()
        {
            if (equipmentBonusText == null)
                return;

            if (!GameProductSettings.IsWormDodgeContentEnabled)
            {
                equipmentBonusText.text = string.Empty;
                return;
            }

            var stats = WormDodgeStatsAggregator.Compute();
            var parts = new System.Collections.Generic.List<string>();
            if (stats.totalCoinMul > 0.001f)
                parts.Add($"金币+{stats.totalCoinMul * 100f:0.#}%");
            if (stats.totalMoveSpeedMul > 0.001f)
                parts.Add($"移速+{stats.totalMoveSpeedMul * 100f:0.#}%");
            if (stats.totalMoveSpeedAdd > 0.01f)
                parts.Add($"移速+{stats.totalMoveSpeedAdd:0.##}");

            equipmentBonusText.text = parts.Count > 0 ? string.Join("  ", parts) : "装备无加成";
        }

        void ApplyProductModeVisibility() =>
            gameObject.SetActive(GameplayModuleContext.IsModuleActive(GameplayModule.WormDodge));
    }
}
