using System;
using Game.Audio;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>超市收纳技能统一入口：冷却、升级等级与效果分发。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeSkillController : MonoBehaviour
    {
        [SerializeField] private SupermarketOrganizeService service;
        [SerializeField] private SupermarketCrosshairInteractor crosshair;
        [SerializeField] private Transform playerTransform;

        readonly float[] _cooldownEndsAt = new float[4];
        float _xRayEndsAt;

        public event Action<SupermarketSkillId> SkillUsed;
        public event Action<SupermarketSkillId> SkillFailed;
        public event Action Changed;

        public bool IsXRayActive => Time.time < _xRayEndsAt;

        void Awake()
        {
            if (playerTransform == null)
                playerTransform = transform;
        }

        void Update()
        {
            if (IsXRayActive && crosshair != null)
                crosshair.ForceRefreshShelfPresentation();
        }

        public void Configure(
            SupermarketOrganizeService organizeService,
            SupermarketCrosshairInteractor interactor,
            Transform playerRoot = null)
        {
            service = organizeService;
            crosshair = interactor;
            if (playerRoot != null)
                playerTransform = playerRoot;
            crosshair?.SetSkillController(this);
        }

        public float GetCooldownRemaining(SupermarketSkillId skillId)
        {
            var end = _cooldownEndsAt[(int)skillId];
            return Mathf.Max(0f, end - Time.time);
        }

        public bool IsReady(SupermarketSkillId skillId) =>
            IsSkillUnlocked(skillId) && GetCooldownRemaining(skillId) <= 0f;

        public bool IsSkillUnlocked(SupermarketSkillId skillId) =>
            SupermarketOrganizeProgressionService.GetSkillLevel(skillId) > 0;

        public bool TryUseSkill(SupermarketSkillId skillId)
        {
            if (service == null || !service.IsGameRunning)
            {
                SkillFailed?.Invoke(skillId);
                return false;
            }

            if (!IsSkillUnlocked(skillId))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiError);
                SkillFailed?.Invoke(skillId);
                return false;
            }

            if (!IsReady(skillId))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiError);
                SkillFailed?.Invoke(skillId);
                return false;
            }

            var used = skillId switch
            {
                SupermarketSkillId.Scanner => TryUseScanner(),
                SupermarketSkillId.Magnet => TryUseMagnet(),
                SupermarketSkillId.XRay => TryUseXRay(),
                SupermarketSkillId.AutoStocker => TryUseAutoStocker(),
                _ => false,
            };

            if (!used)
            {
                SkillFailed?.Invoke(skillId);
                return false;
            }

            var cooldown = SupermarketOrganizeProgressionService.GetSkillCooldownSeconds(skillId);
            _cooldownEndsAt[(int)skillId] = Time.time + cooldown;

            SkillUsed?.Invoke(skillId);
            Changed?.Invoke();
            return true;
        }

        bool TryUseScanner()
        {
            if (service.CurrentItem == null)
                return false;

            var config = SupermarketOrganizeProgressionConfig.ResolveRuntime();
            var duration = config != null ? config.scannerHighlightDurationSeconds : 8f;
            service.ActivateScannerHighlight(duration);
            return true;
        }

        bool TryUseMagnet()
        {
            if (service.HandleZone != null && service.HandleZone.IsFull)
                return false;

            var config = SupermarketOrganizeProgressionConfig.ResolveRuntime();
            var radius = config != null ? config.magnetPullRadius : 10f;
            var origin = playerTransform != null ? playerTransform.position : transform.position;
            return service.TryMagnetPull(origin, radius) > 0;
        }

        bool TryUseXRay()
        {
            if (service.CurrentItem == null)
                return false;

            var config = SupermarketOrganizeProgressionConfig.ResolveRuntime();
            var duration = config != null ? config.xRayTipDurationSeconds : 6f;
            _xRayEndsAt = Time.time + duration;
            crosshair?.ForceRefreshShelfPresentation();
            return true;
        }

        bool TryUseAutoStocker()
        {
            return service.TryAutoStockAllHeldItems() > 0;
        }
    }
}
