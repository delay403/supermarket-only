using System.Collections.Generic;
using Game.Audio;
using Game.Hud;
using Game.UI.ViewModels;
using UnityEngine;

namespace Game.UI.Framework
{
    public static class UiCoordinator
    {
        static readonly Dictionary<UiPanelId, IUiPanelController> Panels =
            new Dictionary<UiPanelId, IUiPanelController>();

        public static HudController Hud { get; private set; }

        public static void RegisterHud(HudController hud) => Hud = hud;

        public static void UnregisterHud(HudController hud)
        {
            if (Hud == hud)
                Hud = null;
        }

        public static void Register(IUiPanelController panel)
        {
            if (panel != null && panel.PanelId != UiPanelId.None)
                Panels[panel.PanelId] = panel;
        }

        public static void Unregister(IUiPanelController panel)
        {
            if (panel != null && Panels.TryGetValue(panel.PanelId, out var existing) && existing == panel)
                Panels.Remove(panel.PanelId);
        }

        public static bool TryGet(UiPanelId id, out IUiPanelController panel) =>
            Panels.TryGetValue(id, out panel);

        public static void Open(UiPanelId id, UiOpenPayload payload = null)
        {
            if (!TryGet(id, out var panel))
            {
                Debug.LogWarning($"UiCoordinator: no panel registered for {id}");
                return;
            }

            panel.Open(payload);
            if (id != UiPanelId.SupermarketOrganizeResult)
                AudioManager.Instance?.PlaySfx(SfxId.UiPanelOpen);
        }

        public static void Close(UiPanelId id)
        {
            if (TryGet(id, out var panel))
                panel.Close();
        }
    }

    public static class UiRouter
    {
        public static void OpenSettings() => UiCoordinator.Open(UiPanelId.Settings);
        public static void CloseSettings() => UiCoordinator.Close(UiPanelId.Settings);
        public static void OpenCoinsHub() => UiCoordinator.Open(UiPanelId.CoinsHub);
        public static void OpenFreeCoinsOffer() => OpenCoinsHub();
        public static void OpenSupermarketOrganizeUpgrade() =>
            UiCoordinator.Open(UiPanelId.SupermarketOrganizeUpgrade);
        public static void OpenSupermarketOrganizeSweepRobot() =>
            UiCoordinator.Open(UiPanelId.SupermarketOrganizeSweepRobot);
        public static void OpenSupermarketOrganizeHome() =>
            UiCoordinator.Open(UiPanelId.SupermarketOrganizeHome);
        public static void OpenSupermarketOrganizeStamina() =>
            UiCoordinator.Open(UiPanelId.SupermarketOrganizeStamina);
        public static void OpenSupermarketOrganizeResult(SupermarketOrganizeResultViewModel model) =>
            UiCoordinator.Open(
                UiPanelId.SupermarketOrganizeResult,
                new UiOpenPayload { SupermarketOrganizeResult = model });
    }
}
