using Game.Economy;
using Game.Save;
using Game.UI.Framework;
using UnityEngine;

namespace Game.Hud
{
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] GameHudUguiPanel uguiHud;

        long _lastCoins = long.MinValue;
        long _lastGems = long.MinValue;

        void Awake()
        {
            if (uguiHud == null)
                uguiHud = GetComponent<GameHudUguiPanel>();
        }

        void OnEnable()
        {
            UiCoordinator.RegisterHud(this);
            EconomyDisplayEvents.Changed += OnEconomyChanged;
        }

        void Start() => RefreshEconomyDisplay();

        void OnDisable()
        {
            EconomyDisplayEvents.Changed -= OnEconomyChanged;
            UiCoordinator.UnregisterHud(this);
        }

        void OnEconomyChanged() => SyncEconomy(false);

        void SyncEconomy(bool force)
        {
            var data = SaveService.Data;
            if (data == null || uguiHud == null)
                return;
            if (!force && data.coins == _lastCoins && data.gems == _lastGems)
                return;
            var decreased = _lastCoins != long.MinValue &&
                            (data.coins < _lastCoins || data.gems < _lastGems);
            _lastCoins = data.coins;
            _lastGems = data.gems;
            uguiHud.SetEconomyDisplay(_lastCoins, _lastGems, 0L, force || decreased);
        }

        public void RefreshEconomyDisplay() => SyncEconomy(true);

        public void AddCoinsDisplay(long delta, bool immediate = true)
        {
            if (delta <= 0 || SaveService.Data == null)
                return;
            ModeSaveAccess.AddCoins(delta);
            SaveService.Save();
            _lastCoins = SaveService.Data.coins;
            _lastGems = SaveService.Data.gems;
            uguiHud?.AddCoinsToDisplay(delta, immediate);
            EconomyDisplayEvents.Raise();
        }

        public void RefreshBuffDisplays() { }
    }
}
