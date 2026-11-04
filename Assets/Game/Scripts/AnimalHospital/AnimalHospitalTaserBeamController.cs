using Game.AnimalHospital.Config;
using Game.VFX;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 装备电击器并按住交互时，在发射点与瞄准目标之间维持 FX_Electricity 电弧连接。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalTaserBeamController : MonoBehaviour
    {
        [SerializeField] private Transform beamOrigin;
        [SerializeField] private float beamStartForwardOffset = 0.45f;
        [SerializeField] private float beamStartRightOffset = 0.12f;
        [SerializeField] private float beamEndHeightOffset = 1f;

        AnimalHospitalCrosshairInteractor _crosshair;
        AnimalHospitalService _service;
        AnimalHospitalConfig _config;
        ParticleBeamConnector _connector;
        GameObject _beamInstance;
        bool _beamVisible;

        void Awake()
        {
            _crosshair = GetComponent<AnimalHospitalCrosshairInteractor>();
            beamOrigin ??= Camera.main != null ? Camera.main.transform : transform;
        }

        public void Configure(AnimalHospitalService service, AnimalHospitalConfig config, Camera gameplayCamera = null)
        {
            if (_service != null)
                _service.Props.HeldPropChanged -= OnPropChanged;

            _service = service;
            _config = config;
            if (gameplayCamera != null)
                beamOrigin = gameplayCamera.transform;
            else if (beamOrigin == null && Camera.main != null)
                beamOrigin = Camera.main.transform;

            if (_service != null && isActiveAndEnabled)
                _service.Props.HeldPropChanged += OnPropChanged;
        }

        void OnEnable()
        {
            if (_crosshair != null)
            {
                _crosshair.HoldProgressChanged += OnHoldChanged;
                _crosshair.AimChanged += OnAimChanged;
            }

            if (_service != null)
                _service.Props.HeldPropChanged += OnPropChanged;
        }

        void OnDisable()
        {
            if (_crosshair != null)
            {
                _crosshair.HoldProgressChanged -= OnHoldChanged;
                _crosshair.AimChanged -= OnAimChanged;
            }

            if (_service != null)
                _service.Props.HeldPropChanged -= OnPropChanged;

            HideBeam();
        }

        void LateUpdate()
        {
            if (!_beamVisible)
                return;

            if (!ShouldShowBeam())
            {
                HideBeam();
                return;
            }

            UpdateBeamEndpoints();
        }

        void OnHoldChanged(AnimalHospitalInteractable target, float progress, float required) =>
            RefreshBeamVisibility();

        void OnAimChanged(AnimalHospitalInteractable target) => RefreshBeamVisibility();

        void OnPropChanged(AnimalHospitalPropId propId) => RefreshBeamVisibility();

        void RefreshBeamVisibility()
        {
            if (ShouldShowBeam())
                ShowBeam();
            else
                HideBeam();
        }

        bool ShouldShowBeam()
        {
            if (_crosshair == null || _service == null || !_crosshair.IsHolding)
                return false;

            if (_service.Props.HeldProp != AnimalHospitalPropId.Taser)
                return false;

            var target = _crosshair.AimTarget;
            return target != null && IsElectricTarget(target);
        }

        static bool IsElectricTarget(AnimalHospitalInteractable target)
        {
            if (target == null)
                return false;

            if (target.Kind == AnimalHospitalInteractableKind.Shadow)
                return true;

            var patient = target.GetComponentInParent<AnimalHospitalPatient>();
            return patient != null && patient.IsAttacking;
        }

        void ShowBeam()
        {
            EnsureBeamInstance();
            if (_connector == null)
                return;

            _beamVisible = true;
            _connector.BeamActive = true;
            UpdateBeamEndpoints();
        }

        void HideBeam()
        {
            _beamVisible = false;
            if (_connector != null)
                _connector.BeamActive = false;
        }

        void UpdateBeamEndpoints()
        {
            if (_connector == null || beamOrigin == null || _crosshair == null)
                return;

            var target = _crosshair.AimTarget;
            if (target == null)
                return;

            var origin = beamOrigin;
            var start = origin.position
                        + origin.forward * beamStartForwardOffset
                        + origin.right * beamStartRightOffset;
            var end = ResolveTargetPoint(target);
            _connector.SetWorldEndpoints(start, end);
        }

        Vector3 ResolveTargetPoint(AnimalHospitalInteractable target)
        {
            var height = _config != null ? _config.taserBeamEndHeightOffset : beamEndHeightOffset;
            var offset = Vector3.up * height;

            var shadow = target.GetComponentInParent<AnimalHospitalShadowAi>();
            if (shadow != null)
                return shadow.ActorTransform.position + offset;

            var patient = target.GetComponentInParent<AnimalHospitalPatient>();
            if (patient != null)
                return patient.ActorTransform.position + offset;

            return target.transform.position + offset;
        }

        void EnsureBeamInstance()
        {
            if (_beamInstance != null && _connector != null)
                return;

            var prefab = _config != null ? _config.taserElectricityPrefab : null;
            if (prefab == null)
                return;

            _beamInstance = Instantiate(prefab);
            _beamInstance.name = $"{prefab.name}_TaserBeam";
            _connector = _beamInstance.GetComponent<ParticleBeamConnector>();
            if (_connector == null)
                _connector = _beamInstance.AddComponent<ParticleBeamConnector>();

            _connector.BeamActive = false;
        }
    }
}
