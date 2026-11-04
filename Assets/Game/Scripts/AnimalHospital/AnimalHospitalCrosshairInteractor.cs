using System;
using Game.AnimalHospital.Config;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>屏幕中心准星射线交互，支持按住进度条完成。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalCrosshairInteractor : MonoBehaviour
    {
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private AnimalHospitalService service;
        [SerializeField] private AnimalHospitalHighlightController highlights;
        [SerializeField] private float maxDistance = 6f;
        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private KeyCode holdKey = KeyCode.E;

        AnimalHospitalConfig _config;
        AnimalHospitalInteractable _aimTarget;
        AnimalHospitalPatient _aimPatient;
        float _holdProgress;
        float _holdRequired = 1f;
        bool _holding;
        bool _hudHolding;

        public event Action<AnimalHospitalInteractable, float, float> HoldProgressChanged;
        public event Action<AnimalHospitalInteractable> AimChanged;
        public event Action<AnimalHospitalPatient> AimPatientChanged;

        public AnimalHospitalInteractable AimTarget => _aimTarget;
        public AnimalHospitalPatient AimPatient => _aimPatient;
        public float HoldNormalized => _holdRequired > 0f ? Mathf.Clamp01(_holdProgress / _holdRequired) : 0f;
        public bool IsHolding => _holding;

        public void Configure(
            Camera cam,
            AnimalHospitalService hospitalService,
            AnimalHospitalHighlightController highlightController,
            AnimalHospitalConfig config)
        {
            raycastCamera = cam;
            service = hospitalService;
            highlights = highlightController;
            _config = config;
            if (config != null)
            {
                config.EnsureDefaultInteractableMask();
                maxDistance = config.raycastMaxDistance;
                interactableMask = config.interactableRaycastMask;
            }
        }

        public void SetHudHolding(bool holding) => _hudHolding = holding;

        void Update()
        {
            if (raycastCamera == null)
                raycastCamera = Camera.main;
            if (raycastCamera == null || service == null || !service.IsGameRunning)
            {
                if (_aimTarget != null || _aimPatient != null)
                {
                    _aimTarget = null;
                    _aimPatient = null;
                    AimChanged?.Invoke(null);
                    AimPatientChanged?.Invoke(null);
                    highlights?.SetAimTarget(null);
                }

                ResetHold();
                return;
            }

            UpdateAim();
            UpdateHoldInput();
            TickHold();
        }

        void UpdateAim()
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var ray = raycastCamera.ScreenPointToRay(center);
            AnimalHospitalInteractable hitTarget = null;
            AnimalHospitalPatient hitPatient = null;

            // RaycastAll：优先病人，避免中间遮挡导致 aim 为空（引导高亮仍在，表现为“看着却点不了”）。
            var hits = Physics.RaycastAll(ray, maxDistance, interactableMask, QueryTriggerInteraction.Collide);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                AnimalHospitalInteractable fallback = null;
                for (var i = 0; i < hits.Length; i++)
                {
                    var col = hits[i].collider;
                    if (col == null)
                        continue;

                    if (hitPatient == null)
                    {
                        var patient = col.GetComponentInParent<AnimalHospitalPatient>();
                        if (patient != null && patient.IsAlive)
                            hitPatient = patient;
                    }

                    var candidate = ResolveInteractable(col);
                    if (candidate == null)
                        continue;

                    if (candidate.Kind == AnimalHospitalInteractableKind.Patient)
                    {
                        hitTarget = candidate;
                        break;
                    }

                    fallback ??= candidate;
                }

                hitTarget ??= fallback;
                if (hitPatient == null && hitTarget != null)
                    hitPatient = hitTarget.GetComponentInParent<AnimalHospitalPatient>();
            }

            if (hitPatient == null && service != null && raycastCamera != null)
                hitPatient = service.FindPatientInAimCone(ray, maxDistance, 12f);

            if (_aimTarget != hitTarget)
            {
                _aimTarget = hitTarget;
                AimChanged?.Invoke(_aimTarget);
                ResetHoldProgressOnly();
            }

            if (_aimPatient != hitPatient)
            {
                _aimPatient = hitPatient;
                AimPatientChanged?.Invoke(_aimPatient);
            }

            highlights?.SetAimTarget(_aimTarget);
        }

        void UpdateHoldInput()
        {
            // 仅 HUD 交互按钮 / 键盘 E：禁止点击屏幕空白处触发世界交互。
            var wantHold = _hudHolding || Input.GetKey(holdKey);

            if (wantHold && _aimTarget != null)
            {
                if (!_holding)
                    BeginHold();
            }
            else if (_holding && !wantHold)
            {
                ResetHold();
            }
        }

        public bool TryGetAimHitPoint(out Vector3 point)
        {
            point = default;
            if (raycastCamera == null)
                return false;

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var ray = raycastCamera.ScreenPointToRay(center);
            if (!Physics.Raycast(ray, out var hit, maxDistance, interactableMask, QueryTriggerInteraction.Collide))
                return false;

            point = hit.point;
            return true;
        }

        void TickHold()
        {
            if (!_holding || _aimTarget == null)
                return;

            if (!_aimTarget.CanInteract(service))
            {
                ResetHold();
                return;
            }

            _holdProgress += Time.deltaTime;
            HoldProgressChanged?.Invoke(_aimTarget, _holdProgress, _holdRequired);

            if (_holdProgress >= _holdRequired)
            {
                var target = _aimTarget;
                ResetHold();
                target.OnInteractionCompleted(service);
            }
        }

        void BeginHold()
        {
            _holding = true;
            _holdProgress = 0f;
            _holdRequired = _aimTarget != null ? _aimTarget.ResolveHoldSeconds(_config) : 1f;
            HoldProgressChanged?.Invoke(_aimTarget, _holdProgress, _holdRequired);
        }

        void ResetHold()
        {
            if (_holding || _holdProgress > 0f)
            {
                _holding = false;
                _holdProgress = 0f;
                HoldProgressChanged?.Invoke(_aimTarget, 0f, _holdRequired);
            }

            _holding = false;
            _holdProgress = 0f;
        }

        void ResetHoldProgressOnly()
        {
            _holdProgress = 0f;
            if (_holding && _aimTarget != null)
                _holdRequired = _aimTarget.ResolveHoldSeconds(_config);
            HoldProgressChanged?.Invoke(_aimTarget, 0f, _holdRequired);
        }

        AnimalHospitalInteractable ResolveInteractable(Collider hitCollider)
        {
            if (hitCollider == null || service == null)
                return null;

            // 自下而上找第一个当前可交互对象，避免已失效的入院单挡住病人。
            var t = hitCollider.transform;
            while (t != null)
            {
                var interactable = t.GetComponent<AnimalHospitalInteractable>();
                if (interactable != null && interactable.CanInteract(service))
                    return interactable;
                t = t.parent;
            }

            return null;
        }
    }
}
