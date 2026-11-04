using System.Collections;
using Game.AnimalHospital.Config;
using Game.VFX;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 手持道具攻击：电棍点按挥击；电击器/灭火器/冷冻枪按住射线特效并周期性伤害；手枪按住连射+后坐力。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalPropCombatController : MonoBehaviour
    {
        [SerializeField] private float beamStartForwardOffset = 0.45f;
        [SerializeField] private float beamStartRightOffset = 0.12f;
        [SerializeField] private float batonThrustDistance = 0.55f;
        [SerializeField] private float batonThrustSeconds = 0.12f;
        [SerializeField] private float pistolRecoilPitchDegrees = 8f;
        [SerializeField] private float pistolRecoilRecoverSeconds = 0.12f;

        AnimalHospitalService _service;
        AnimalHospitalConfig _config;
        AnimalHospitalCrosshairInteractor _crosshair;
        AnimalHospitalPropHandPresenter _hand;
        Transform _beamOrigin;

        bool _attackHeld;
        float _holdStartedAt = -1f;
        float _nextDamageAt;
        float _nextShotAt;
        float _batonBusyUntil;
        float _pistolRecoilUntil;
        bool _holdDamageArmed;
        float _extinguishHoldAccum;
        Quaternion _handBaseLocalRotation = Quaternion.identity;
        Vector3 _handBaseLocalPosition;
        bool _hasHandBasePose;
        Coroutine _batonRoutine;

        ParticleBeamConnector _connector;
        GameObject _beamInstance;
        AnimalHospitalPropId _beamProp = AnimalHospitalPropId.None;
        bool _beamVisible;

        public bool IsAttackHeld => _attackHeld;

        public bool CanShowAttackButton =>
            _service != null
            && _service.Props.HeldProp != AnimalHospitalPropId.None
            && (CanExtinguishBurningWard()
                || (_crosshair != null
                    && _crosshair.AimPatient != null
                    && _crosshair.AimPatient.IsAlive));

        public bool CanShowHolsterButton =>
            _service != null && _service.Props.HeldProp != AnimalHospitalPropId.None;

        public bool IsTapAttackProp =>
            _service != null && _service.Props.HeldProp == AnimalHospitalPropId.StunBaton;

        public void Configure(
            AnimalHospitalService service,
            AnimalHospitalConfig config,
            AnimalHospitalCrosshairInteractor crosshair,
            AnimalHospitalPropHandPresenter hand,
            Camera gameplayCamera)
        {
            if (_service != null)
                _service.Props.HeldPropChanged -= OnHeldPropChanged;

            _service = service;
            _config = config;
            _crosshair = crosshair;
            _hand = hand;
            _beamOrigin = gameplayCamera != null ? gameplayCamera.transform : transform;

            if (_service != null)
                _service.Props.HeldPropChanged += OnHeldPropChanged;
        }

        public void SetAttackHeld(bool held)
        {
            if (_attackHeld == held)
                return;

            _attackHeld = held;
            if (!held)
            {
                HideBeam();
                AnimalHospitalAudio.Resolve()?.SetWeaponAttackLoop(AnimalHospitalPropId.None, false);
                _holdStartedAt = -1f;
                _holdDamageArmed = false;
                _extinguishHoldAccum = 0f;
                return;
            }

            _holdStartedAt = Time.time;
            _holdDamageArmed = false;
            _extinguishHoldAccum = 0f;
            _nextDamageAt = Time.time + ResolveHoldDamageDelay();

            if (IsTapAttackProp)
                TryFireStunBaton();
            else
                TryBeginContinuousAttack();
        }

        public void HolsterHeldProp()
        {
            SetAttackHeld(false);
            if (_service == null)
                return;

            if (_service.Props.Holster())
                _service.ShowToast("道具已收起，需重新交互场景道具才能装备。");
        }

        void OnHeldPropChanged(AnimalHospitalPropId _)
        {
            _hasHandBasePose = false;
            _attackHeld = false;
            HideBeam();
            if (_batonRoutine != null)
            {
                StopCoroutine(_batonRoutine);
                _batonRoutine = null;
            }
        }

        void OnDisable()
        {
            if (_service != null)
                _service.Props.HeldPropChanged -= OnHeldPropChanged;

            HideBeam();
            _attackHeld = false;
            _hasHandBasePose = false;
        }

        void Update()
        {
            if (_service == null || _service.Props.HeldProp == AnimalHospitalPropId.None)
            {
                if (_beamVisible)
                    HideBeam();
                if (_attackHeld)
                    SetAttackHeld(false);
                return;
            }

            TickHandPose();

            // 准星离开目标 / 攻击按钮被隐藏时，必须松开，避免“未按住仍持续攻击”。
            if (_attackHeld && !CanAttackCurrentTarget())
            {
                SetAttackHeld(false);
                return;
            }

            if (!_attackHeld)
            {
                if (_beamVisible)
                    HideBeam();
                AnimalHospitalAudio.Resolve()?.SetWeaponAttackLoop(AnimalHospitalPropId.None, false);
                return;
            }

            switch (_service.Props.HeldProp)
            {
                case AnimalHospitalPropId.Taser:
                case AnimalHospitalPropId.FireExtinguisher:
                case AnimalHospitalPropId.FreezeGun:
                    TickBeamWeapon(_service.Props.HeldProp);
                    break;
                case AnimalHospitalPropId.Pistol:
                    TickPistol();
                    break;
            }
        }

        void LateUpdate()
        {
            if (!_beamVisible || _connector == null)
                return;

            if (!CanAttackCurrentTarget())
            {
                HideBeam();
                return;
            }

            UpdateBeamEndpoints();
        }

        void TryBeginContinuousAttack()
        {
            if (!CanAttackCurrentTarget())
                return;

            var prop = _service.Props.HeldProp;
            if (prop == AnimalHospitalPropId.Taser
                || prop == AnimalHospitalPropId.FireExtinguisher
                || prop == AnimalHospitalPropId.FreezeGun)
            {
                if (CanExtinguishBurningWard())
                {
                    ShowBeam(prop);
                    AnimalHospitalAudio.Resolve()?.SetWeaponAttackLoop(prop, true);
                    _extinguishHoldAccum = 0f;
                    return;
                }

                // 按下立刻出特效，但伤害需持续按住满 delay 秒。
                ShowBeam(prop);
                AnimalHospitalAudio.Resolve()?.SetWeaponAttackLoop(prop, true);
                _holdDamageArmed = false;
                _nextDamageAt = Time.time + ResolveHoldDamageDelay();
            }
            else if (prop == AnimalHospitalPropId.Pistol)
            {
                _holdDamageArmed = false;
                _nextShotAt = Time.time + ResolvePistolHoldDelay();
            }
        }

        void TickBeamWeapon(AnimalHospitalPropId prop)
        {
            if (CanExtinguishBurningWard())
            {
                TickWardExtinguish(prop);
                return;
            }

            if (!CanAttackPatient())
            {
                HideBeam();
                return;
            }

            ShowBeam(prop);
            if (Time.time < _nextDamageAt)
                return;

            ApplyDamageToAimPatient(prop);
            _holdDamageArmed = true;
            _nextDamageAt = Time.time + ResolveBeamDamageInterval();
        }

        void TickWardExtinguish(AnimalHospitalPropId prop)
        {
            if (!CanExtinguishBurningWard())
            {
                HideBeam();
                AnimalHospitalAudio.Resolve()?.SetWeaponAttackLoop(AnimalHospitalPropId.None, false);
                _extinguishHoldAccum = 0f;
                return;
            }

            ShowBeam(prop);
            AnimalHospitalAudio.Resolve()?.SetWeaponAttackLoop(prop, true);

            _extinguishHoldAccum += Time.deltaTime;
            if (_extinguishHoldAccum < ResolveWardExtinguishHoldSeconds())
                return;

            var ward = _service.FindBurningWardContainingPlayer();
            if (ward != null && _service.Events.TryExtinguishWard(ward))
                _service.ShowToast("病房火势已扑灭。");

            _extinguishHoldAccum = 0f;
            SetAttackHeld(false);
        }

        void TickPistol()
        {
            if (!CanAttackCurrentTarget())
                return;

            if (Time.time < _nextShotAt)
                return;

            ApplyDamageToAimPatient(AnimalHospitalPropId.Pistol);
            ApplyPistolRecoil();
            AnimalHospitalAudio.Resolve()?.PlayPistolShot();
            _holdDamageArmed = true;
            _nextShotAt = Time.time + ResolvePistolInterval();
        }

        void TryFireStunBaton()
        {
            if (Time.time < _batonBusyUntil)
                return;
            if (!CanAttackCurrentTarget())
                return;

            ApplyDamageToAimPatient(AnimalHospitalPropId.StunBaton);
            AnimalHospitalAudio.Resolve()?.PlayBatonSwing();
            if (_batonRoutine != null)
                StopCoroutine(_batonRoutine);
            _batonRoutine = StartCoroutine(BatonThrustRoutine());
            var speed = _config != null ? Mathf.Max(0.05f, _config.stunBatonAttackIntervalSeconds) : 0.45f;
            _batonBusyUntil = Time.time + speed;
        }

        IEnumerator BatonThrustRoutine()
        {
            var visual = _hand != null ? _hand.CurrentHandVisualTransform : null;
            if (visual == null)
                yield break;

            CaptureHandBasePose(visual);
            var duration = Mathf.Max(0.05f, batonThrustSeconds);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var thrust = Mathf.Sin(t * Mathf.PI) * batonThrustDistance;
                visual.localPosition = _handBaseLocalPosition + Vector3.forward * thrust;
                yield return null;
            }

            visual.localPosition = _handBaseLocalPosition;
            _batonRoutine = null;
        }

        void ApplyPistolRecoil()
        {
            var visual = _hand != null ? _hand.CurrentHandVisualTransform : null;
            if (visual == null)
                return;

            CaptureHandBasePose(visual);
            visual.localRotation = _handBaseLocalRotation * Quaternion.Euler(-pistolRecoilPitchDegrees, 0f, 0f);
            _pistolRecoilUntil = Time.time + Mathf.Max(0.05f, pistolRecoilRecoverSeconds);
        }

        void TickHandPose()
        {
            var visual = _hand != null ? _hand.CurrentHandVisualTransform : null;
            if (visual == null || !_hasHandBasePose)
                return;

            if (Time.time >= _pistolRecoilUntil
                && _service.Props.HeldProp != AnimalHospitalPropId.StunBaton)
            {
                visual.localRotation = Quaternion.Slerp(
                    visual.localRotation,
                    _handBaseLocalRotation,
                    Time.deltaTime * 12f);
            }
        }

        void CaptureHandBasePose(Transform visual)
        {
            if (visual == null)
                return;

            if (!_hasHandBasePose)
            {
                _handBaseLocalPosition = visual.localPosition;
                _handBaseLocalRotation = visual.localRotation;
                _hasHandBasePose = true;
            }
        }

        bool CanExtinguishBurningWard() =>
            _service != null
            && _service.Props.HasExtinguisherOrFreeze
            && _service.FindBurningWardContainingPlayer() != null;

        bool CanAttackPatient()
        {
            var patient = _crosshair != null ? _crosshair.AimPatient : null;
            return patient != null && patient.IsAlive;
        }

        bool CanAttackCurrentTarget() => CanExtinguishBurningWard() || CanAttackPatient();

        void ApplyDamageToAimPatient(AnimalHospitalPropId prop)
        {
            var patient = _crosshair != null ? _crosshair.AimPatient : null;
            if (patient == null || !patient.IsAlive || _service == null)
                return;

            patient.TakeDamage(_service.Props.AttackDamage(_config));

            if (prop == AnimalHospitalPropId.Taser || prop == AnimalHospitalPropId.FreezeGun)
            {
                var mult = _config != null ? Mathf.Clamp01(_config.propSlowSpeedMultiplier) : 0.3f;
                var dur = _config != null ? Mathf.Max(0.1f, _config.propSlowDurationSeconds) : 2f;
                patient.ApplyMoveSpeedSlow(mult, dur);
            }
        }

        float ResolveHoldDamageDelay() =>
            _config != null ? Mathf.Max(0.1f, _config.propHoldDamageDelaySeconds) : 2f;

        float ResolvePistolHoldDelay() =>
            _config != null ? Mathf.Max(0f, _config.pistolHoldDamageDelaySeconds) : 0f;

        float ResolveBeamDamageInterval() =>
            _config != null ? Mathf.Max(0.1f, _config.propBeamDamageIntervalSeconds) : 2f;

        float ResolvePistolInterval() =>
            _config != null ? Mathf.Max(0.05f, _config.pistolFireIntervalSeconds) : 0.35f;

        float ResolveWardExtinguishHoldSeconds() =>
            _config != null ? Mathf.Max(0.5f, _config.wardExtinguishHoldSeconds) : 2f;

        void ShowBeam(AnimalHospitalPropId prop)
        {
            EnsureBeamInstance(prop);
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
            // 松手或丢准星时停武器循环；点按电棍不走这里。
            if (!_attackHeld)
                AnimalHospitalAudio.Resolve()?.SetWeaponAttackLoop(AnimalHospitalPropId.None, false);
        }

        void UpdateBeamEndpoints()
        {
            if (_connector == null || _beamOrigin == null)
                return;

            var start = _beamOrigin.position
                        + _beamOrigin.forward * beamStartForwardOffset
                        + _beamOrigin.right * beamStartRightOffset;

            Vector3 end;
            if (CanExtinguishBurningWard())
            {
                end = start + _beamOrigin.forward * 4f;
                if (_crosshair != null && _crosshair.TryGetAimHitPoint(out var hit))
                    end = hit;
            }
            else
            {
                var patient = _crosshair != null ? _crosshair.AimPatient : null;
                if (patient == null)
                    return;

                var height = _config != null ? _config.taserBeamEndHeightOffset : 1f;
                end = patient.ActorTransform.position + Vector3.up * height;
                if (_crosshair != null && _crosshair.TryGetAimHitPoint(out var hit))
                    end = hit;
            }

            _connector.SetWorldEndpoints(start, end);
        }

        void EnsureBeamInstance(AnimalHospitalPropId prop)
        {
            if (_beamInstance != null && _connector != null && _beamProp == prop)
                return;

            HideBeam();
            if (_beamInstance != null)
            {
                Destroy(_beamInstance);
                _beamInstance = null;
                _connector = null;
            }

            var prefab = ResolveBeamPrefab(prop);
            if (prefab == null)
                return;

            _beamInstance = Instantiate(prefab);
            _beamInstance.name = $"{prefab.name}_PropBeam";
            _connector = _beamInstance.GetComponent<ParticleBeamConnector>()
                         ?? _beamInstance.GetComponentInChildren<ParticleBeamConnector>()
                         ?? _beamInstance.AddComponent<ParticleBeamConnector>();
            _connector.BeamActive = false;
            _beamProp = prop;
        }

        GameObject ResolveBeamPrefab(AnimalHospitalPropId prop)
        {
            if (_config == null)
                return null;

            return prop switch
            {
                AnimalHospitalPropId.Taser => _config.taserElectricityPrefab,
                AnimalHospitalPropId.FireExtinguisher => _config.fireExtinguisherBeamPrefab != null
                    ? _config.fireExtinguisherBeamPrefab
                    : _config.taserElectricityPrefab,
                AnimalHospitalPropId.FreezeGun => _config.freezeGunBeamPrefab != null
                    ? _config.freezeGunBeamPrefab
                    : _config.taserElectricityPrefab,
                _ => null,
            };
        }
    }
}
