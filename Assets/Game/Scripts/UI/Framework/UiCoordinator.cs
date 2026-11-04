using System.Collections.Generic;
using System;
using UnityEngine;
using Game.Collectibles.Equipment;
using Game.Collectibles.Stats;
using Game.Economy;
using Game.Fishing;
using Game.UI;
using Game.UI.Preview;
using Game.Core;
using Game.WormDodge;
using Game.Hud;
using Game.Audio;
using Game.UI.ViewModels;
using Game.Reward;
using Game.Features.Treadmill;

namespace Game.UI.Framework
{
    /// <summary>
    /// Central registry and open/close entry for UI (UGUI now; FGUI can call the same API from a thin adapter).
    /// </summary>
    public static class UiCoordinator
    {
        private static readonly Dictionary<UiPanelId, IUiPanelController> Panels = new Dictionary<UiPanelId, IUiPanelController>();

        public static HudController Hud { get; private set; }
        public static EquipmentSpawner Equipment { get; private set; }

        public static void RegisterHud(HudController hud)
        {
            Hud = hud;
        }

        public static void UnregisterHud(HudController hud)
        {
            if (Hud == hud) Hud = null;
        }

        public static void RegisterEquipment(EquipmentSpawner spawner)
        {
            Equipment = spawner;
        }

        public static void UnregisterEquipment(EquipmentSpawner spawner)
        {
            if (Equipment == spawner) Equipment = null;
        }

        public static void Register(IUiPanelController panel)
        {
            if (panel == null) return;
            var id = panel.PanelId;
            if (id == UiPanelId.None) return;
            Panels[id] = panel;
        }

        public static void Unregister(IUiPanelController panel)
        {
            if (panel == null) return;
            var id = panel.PanelId;
            if (!Panels.TryGetValue(id, out var existing) || existing != panel) return;
            Panels.Remove(id);
        }

        public static bool TryGet(UiPanelId id, out IUiPanelController panel)
        {
            return Panels.TryGetValue(id, out panel);
        }

        public static void Open(UiPanelId id, UiOpenPayload payload = null)
        {
            if (!TryGet(id, out var panel))
            {
                Debug.LogWarning($"UiCoordinator: no panel registered for {id}");
                return;
            }

            if (GameplayNavigationBlock.ShouldDenyOpeningPanel(id))
            {
                GameplayNavigationBlock.ShowShopNavigationBlockedHint();
                return;
            }

            panel.Open(payload);
            if (id != UiPanelId.Result && id != UiPanelId.WormDodgeResult
                && id != UiPanelId.AnimalHospitalResult && id != UiPanelId.AnimalHospitalDeath)
                AudioManager.Instance?.PlaySfx(SfxId.UiPanelOpen);
        }

        public static void Close(UiPanelId id)
        {
            if (!TryGet(id, out var panel)) return;
            panel.Close();
        }

        /// <summary>Convenience for glide / settlement flow.</summary>
        public static void ShowSettlement(SettlementViewModel model)
        {
            Open(UiPanelId.Result, new UiOpenPayload { Settlement = model });
        }

        /// <summary>
        /// 按存档重实例化宠物/翅膀/拖尾 prefab。优先用 <see cref="RegisterEquipment"/> 的实例；
        /// 若尚未注册（例如执行顺序或 DDOL 场景先起 UI），则回退查找场景中的 <see cref="EquipmentSpawner"/>。
        /// </summary>
        public static void RefreshEquipmentVisuals()
        {
            if (Equipment != null)
                Equipment.RefreshAll();
            else
            {
                var spawner = UnityObjectFinder.FindFirst<EquipmentSpawner>();
                if (spawner != null)
                    spawner.RefreshAll();
                else
                    Debug.LogWarning(
                        "UiCoordinator.RefreshEquipmentVisuals: 未找到 EquipmentSpawner，外观不会刷新。" +
                        "请把 EquipmentSpawner 挂在玩家角色根或子物体上，并在 Awake 前保证场景中存在 PlayerRig（petSocket / wingSocket / trailSocket）。");
            }

            CosmeticPreviewController.NotifyPlayerEquipmentVisualsChanged();
            RefreshFishingPowerStats();
            RefreshDestroyCityStats();
            RefreshWormDodgeStats();
        }

        /// <summary>换装后刷新躲避虫子装备加成。</summary>
        public static void RefreshWormDodgeStats()
        {
            if (!GameProductSettings.IsWormDodgeContentEnabled)
                return;

            var agg = UnityObjectFinder.FindFirst<WormDodgeStatsAggregator>();
            agg?.Recompute();

            var boost = UnityObjectFinder.FindFirst<WormDodgePlayerMotorBoost>();
            boost?.ApplyStats();

            WormDodgeHudSection.RefreshAllInScene();
        }

        /// <summary>换装后刷新摧毁城市燃料加成聚合。</summary>
        public static void RefreshDestroyCityStats()
        {
            if (!GameProductSettings.IsDestroyCityContentEnabled)
                return;

            var agg = UnityObjectFinder.FindFirst<Collectibles.Stats.DestroyCityStatsAggregator>();
            agg?.Recompute();

            var hud = UnityObjectFinder.FindFirst<DestroyCity.DestroyCityHudSection>();
            hud?.RefreshAll();
        }

        /// <summary>换装后刷新钓鱼力量聚合（翅膀/宠物等 modifiers 变更）。</summary>
        public static void RefreshFishingPowerStats()
        {
            if (!GameProductSettings.IsFishingContentEnabled)
                return;

            var player = UnityObjectFinder.FindFirst<PlayerFishingController>();
            if (player != null)
            {
                var agg = player.GetComponent<FishingStatsAggregator>();
                agg?.Recompute();
            }

            UiCoordinator.Hud?.RefreshStrengthDisplay();
        }

        /// <summary>刷新背部展示鱼（FishRoot）。</summary>
        public static void RefreshBackFishVisual()
        {
            if (Equipment != null)
                Equipment.RefreshBackFish();
            else
                UnityObjectFinder.FindFirst<EquipmentSpawner>()?.RefreshBackFish();
        }

        /// <summary>优惠商品发放奖励后，刷新仍打开的相关商店界面。</summary>
        public static void RefreshOpenShopPanelsForDiscountRewards(Economy.Config.DiscountOfferDef def)
        {
            var rewards = DiscountOfferRewardGranter.ResolveRewards(def);
            var needsRod = false;
            var needsWing = false;
            var needsTrail = false;
            var needsPet = false;

            for (var i = 0; i < rewards.Length; i++)
            {
                switch (rewards[i].kind)
                {
                    case Economy.Config.DiscountOfferRewardKind.Rod:
                        needsRod = true;
                        break;
                    case Economy.Config.DiscountOfferRewardKind.Wing:
                        needsWing = true;
                        break;
                    case Economy.Config.DiscountOfferRewardKind.Trail:
                        needsTrail = true;
                        break;
                    case Economy.Config.DiscountOfferRewardKind.Pet:
                        needsPet = true;
                        break;
                }
            }

            if (needsRod && TryGet(UiPanelId.RodShop, out var rodPanel) && rodPanel is RodShopPanel rodShop)
                rodShop.RefreshOwnedCatalogIfOpen();

            if (needsWing && TryGet(UiPanelId.WingShop, out var wingPanel) && wingPanel is WingShopPanel wingShop)
                wingShop.RefreshOwnedCatalogIfOpen();

            if (needsTrail && TryGet(UiPanelId.TrailShop, out var trailPanel) && trailPanel is WingShopPanel trailShop)
                trailShop.RefreshOwnedCatalogIfOpen();

            if (needsPet && TryGet(UiPanelId.Pet, out var petPanel) && petPanel is PetPanel pet)
                pet.RefreshOwnedCatalogIfOpen();
        }

    }

    /// <summary>Typed shortcuts so gameplay code does not scatter enum literals.</summary>
    public static class UiRouter
    {
        public static void OpenPet() => UiCoordinator.Open(UiPanelId.Pet);
        public static void OpenPetHatch() => UiCoordinator.Open(UiPanelId.PetHatch);

        /// <summary>打开孵蛋界面并选中目录中的卡池（<paramref name="catalogIndex"/> 从 0 起）。</summary>
        public static void OpenPetHatchAtCatalogIndex(int catalogIndex) =>
            OpenPetHatchWithPayload(new PetHatchOpenPayload { CatalogIndex = catalogIndex });

        /// <summary>打开孵蛋界面并选中 <see cref="Game.Collectibles.Pets.PetHatchPoolConfig.poolId"/> 匹配的卡池。</summary>
        public static void OpenPetHatchWithPoolId(string poolId) =>
            OpenPetHatchWithPayload(string.IsNullOrWhiteSpace(poolId)
                ? null
                : new PetHatchOpenPayload { PoolId = poolId.Trim() });

        public static void OpenPetHatchWithPayload(PetHatchOpenPayload petHatch)
        {
            if (petHatch == null ||
                (string.IsNullOrWhiteSpace(petHatch.PoolId) && petHatch.CatalogIndex < 0))
            {
                UiCoordinator.Open(UiPanelId.PetHatch);
                return;
            }

            UiCoordinator.Open(UiPanelId.PetHatch, new UiOpenPayload { PetHatch = petHatch });
        }
        public static void OpenWingShop() => UiCoordinator.Open(UiPanelId.WingShop);
        public static void OpenTrailShop() => UiCoordinator.Open(UiPanelId.TrailShop);
        public static void OpenRodShop() => UiCoordinator.Open(UiPanelId.RodShop);
        public static void OpenFishCodex() => UiCoordinator.Open(UiPanelId.FishCodex);
        public static void OpenCharacterShop() => UiCoordinator.Open(UiPanelId.CharacterShop);
        public static void OpenMapSelect() => UiCoordinator.Open(UiPanelId.MapSelect);
        public static void OpenSignIn() => UiCoordinator.Open(UiPanelId.SignIn);
        public static void OpenLuckyWheel() => UiCoordinator.Open(UiPanelId.LuckyWheel);
        public static void OpenFreeCoinsOffer() => OpenCoinsHub();

        public static void OpenCoinsHub() =>
            UiCoordinator.Open(UiPanelId.CoinsHub);
        public static void OpenFreeDiamondsOffer() => UiCoordinator.Open(UiPanelId.FreeDiamondsOffer);
        public static void OpenOnlineReward() => UiCoordinator.Open(UiPanelId.OnlineReward);
        public static void OpenSettings() => UiCoordinator.Open(UiPanelId.Settings);
        public static void OpenDoubleClimbSpeedOffer() => UiCoordinator.Open(UiPanelId.FreeDoubleClimbSpeed);
        public static void OpenDoubleSettlementCoinsOffer() => UiCoordinator.Open(UiPanelId.FreeDoubleSettlementCoins);
        public static void OpenAutoFishingOffer() => UiCoordinator.Open(UiPanelId.FreeAutoFishing);

        public static void OpenFuelProducerUnlockOffer(string producerId) =>
            UiCoordinator.Open(UiPanelId.FreeUnlockFuelProducer, new UiOpenPayload
            {
                FuelProducerUnlock = new FuelProducerUnlockOpenPayload { ProducerId = producerId },
            });

        public static void OpenDiscountOffer(int offerId) =>
            UiCoordinator.Open(UiPanelId.DiscountOffer, new UiOpenPayload
            {
                DiscountOffer = new DiscountOfferOpenPayload { OfferId = offerId },
            });

        public static void OpenTreadmillUnlock(
            TreadmillTier tier,
            TreadmillPanelMode mode,
            bool allowFreeUnlock,
            bool allowGemUnlock,
            Action onStartRequested) =>
            UiCoordinator.Open(UiPanelId.TreadmillUnlock, new UiOpenPayload
            {
                TreadmillUnlock = new TreadmillUnlockOpenPayload
                {
                    Tier = tier,
                    Mode = mode,
                    AllowFreeUnlock = allowFreeUnlock,
                    AllowGemUnlock = allowGemUnlock,
                    OnStartRequested = onStartRequested,
                },
            });

        public static void OpenStrengthTrainingUnlock(
            string groundId,
            StrengthTrainingPanelMode mode,
            bool allowFreeUnlock,
            bool allowGemUnlock,
            Sprite icon,
            Action onStartRequested) =>
            UiCoordinator.Open(UiPanelId.StrengthTrainingUnlock, new UiOpenPayload
            {
                StrengthTrainingUnlock = new StrengthTrainingUnlockOpenPayload
                {
                    GroundId = groundId,
                    Mode = mode,
                    AllowFreeUnlock = allowFreeUnlock,
                    AllowGemUnlock = allowGemUnlock,
                    Icon = icon,
                    OnStartRequested = onStartRequested,
                },
            });

        public static void OpenTreadmillInstantRewardOffer(TreadmillTier tier) =>
            UiCoordinator.Open(UiPanelId.FreeTreadmillInstantReward, new UiOpenPayload
            {
                TreadmillInstantReward = new TreadmillInstantRewardOpenPayload { Tier = tier },
            });

        public static void OpenTreadmillResult(ViewModels.TreadmillResultViewModel model) =>
            UiCoordinator.Open(UiPanelId.TreadmillResult, new UiOpenPayload { TreadmillResult = model });

        public static void OpenWormDodgeResult(ViewModels.WormDodgeResultViewModel model) =>
            UiCoordinator.Open(UiPanelId.WormDodgeResult, new UiOpenPayload { WormDodgeResult = model });

        public static void OpenWormDodgeHome() =>
            UiCoordinator.Open(UiPanelId.WormDodgeHome);

        public static void OpenWormDodgeFloorReward(ViewModels.WormDodgeFloorRewardViewModel model) =>
            UiCoordinator.Open(UiPanelId.WormDodgeFloorReward, new UiOpenPayload { WormDodgeFloorReward = model });

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

        public static void OpenAnimalHospitalResult(AnimalHospitalResultViewModel model) =>
            UiCoordinator.Open(
                UiPanelId.AnimalHospitalResult,
                new UiOpenPayload { AnimalHospitalResult = model });

        public static void OpenAnimalHospitalDeath() =>
            UiCoordinator.Open(UiPanelId.AnimalHospitalDeath);

        /// <summary>
        /// 动物医院咖啡领取确认（场景中需挂 <see cref="Game.UI.FreeCurrencyOfferPanel"/> 且 PanelId=AnimalHospitalCoffeeOffer）。
        /// </summary>
        public static void OpenAnimalHospitalCoffeeOffer(string title, string description, Action<bool> completion)
        {
            UiCoordinator.Open(
                UiPanelId.AnimalHospitalCoffeeOffer,
                new UiOpenPayload
                {
                    RewardDialog = new RewardDialogPayload
                    {
                        Title = title,
                        Description = description,
                        Completion = completion,
                    },
                });
        }

        /// <summary>
        /// 通用激励弹窗（须在场景中注册 <see cref="UiPanelId.RewardDialog"/>；双倍攀爬/双倍金币 Buff 请改用 <see cref="OpenDoubleClimbSpeedOffer"/> / <see cref="OpenDoubleSettlementCoinsOffer"/>）。
        /// </summary>
        public static void OpenRewardDialog(string title, string description, Action<bool> completion)
        {
            UiCoordinator.Open(UiPanelId.RewardDialog,
                new UiOpenPayload
                {
                    RewardDialog = new RewardDialogPayload
                    {
                        Title = title,
                        Description = description,
                        Completion = completion,
                    },
                });
        }

        public static void ClosePet() => UiCoordinator.Close(UiPanelId.Pet);
        public static void CloseResult() => UiCoordinator.Close(UiPanelId.Result);
        public static void CloseSettings() => UiCoordinator.Close(UiPanelId.Settings);
    }
}
