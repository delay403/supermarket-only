using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>特殊事件调度：天花板/病房黑影/着火/走廊粘液/走廊黑影（各事件同时仅一）。</summary>
    public sealed class AnimalHospitalEventManager
    {
        readonly AnimalHospitalService _service;
        readonly AnimalHospitalConfigProxy _proxy;

        float _wardFireTimer;
        float _slimeTimer;
        float _corridorShadowTimer;

        AnimalHospitalCeilingHazard _ceiling;
        AnimalHospitalShadowAi _wardShadow;
        AnimalHospitalWard _burningWard;
        float _burningElapsed;
        AnimalHospitalSlimeHazard _slime;
        AnimalHospitalShadowAi _corridorShadow;

        public bool HasCeilingEvent => _ceiling != null;
        public bool HasWardShadow => _wardShadow != null;
        public bool HasWardFire => _burningWard != null;
        public bool HasCorridorSlime => _slime != null;
        public bool HasCorridorShadow => _corridorShadow != null;
        public AnimalHospitalWard BurningWard => _burningWard;

        public AnimalHospitalEventManager(AnimalHospitalService service)
        {
            _service = service;
            _proxy = new AnimalHospitalConfigProxy(service);
        }

        public void Tick(float dt)
        {
            var cfg = _service.Config;
            if (cfg == null)
                return;

            if (_service.Props.HasExtinguisherOrFreeze)
            {
                _wardFireTimer += dt;
                if (_wardFireTimer >= cfg.wardFireCheckIntervalSeconds)
                {
                    _wardFireTimer = 0f;
                    if (_service.TryConsumeAnomalyEventSlot())
                        TryTriggerWardFire(cfg);
                }
            }

            _slimeTimer += dt;
            if (_slimeTimer >= cfg.corridorSlimeCheckIntervalSeconds)
            {
                _slimeTimer = 0f;
                if (_service.TryConsumeAnomalyEventSlot())
                    TryTriggerCorridorSlime(cfg);
            }

            _corridorShadowTimer += dt;
            if (_corridorShadowTimer >= cfg.corridorShadowCheckIntervalSeconds)
            {
                _corridorShadowTimer = 0f;
                if (_service.TryConsumeAnomalyEventSlot())
                    TryTriggerCorridorShadow(cfg);
            }

            TickWardFire(dt, cfg);
            TickPlayerInBurningWard(dt, cfg);
        }

        public void OnPatientEnteredWard(AnimalHospitalPatient patient, AnimalHospitalWard ward)
        {
            var cfg = _service.Config;
            if (cfg == null || ward == null || patient == null || !patient.IsAlive)
                return;

            if (!ward.IsOccupied || ward.Occupant != patient)
                return;

            if (!HasCeilingEvent && _service.TryConsumeAnomalyEventSlot())
                SpawnCeiling(ward, cfg);

            if (!HasWardShadow && _service.TryConsumeAnomalyEventSlot())
                SpawnWardShadow(ward, cfg);
        }

        public void OnWardPatientCured(AnimalHospitalWard ward)
        {
            if (_ceiling != null && _ceiling.Ward == ward)
            {
                Object.Destroy(_ceiling.gameObject);
                _ceiling = null;
            }
        }

        public bool TryExtinguishWard(AnimalHospitalWard ward)
        {
            if (ward == null || _burningWard != ward)
                return false;

            ClearWardFire();
            return true;
        }

        public bool TryRemoveSlime(AnimalHospitalSlimeHazard slime)
        {
            if (slime == null || _slime != slime)
                return false;

            Object.Destroy(slime.gameObject);
            _slime = null;
            return true;
        }

        public void NotifyShadowDefeated(AnimalHospitalShadowAi shadow)
        {
            if (_wardShadow == shadow)
                _wardShadow = null;
            if (_corridorShadow == shadow)
                _corridorShadow = null;
        }

        public void NotifySlimeExpired(AnimalHospitalSlimeHazard slime)
        {
            if (_slime == slime)
                _slime = null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugTriggerCeilingEvent(AnimalHospitalWard ward = null)
        {
            var cfg = _service.Config;
            if (cfg == null)
                return;

            ward ??= _service.PickRandomOccupiedOrAnyWard();
            if (ward == null)
            {
                Debug.LogWarning("[AnimalHospital] DebugTriggerCeilingEvent: no ward available.");
                return;
            }

            if (HasCeilingEvent)
            {
                Object.Destroy(_ceiling.gameObject);
                _ceiling = null;
            }

            SpawnCeiling(ward, cfg);
            Debug.Log($"[AnimalHospital] Debug: ceiling hazard spawned on ward '{ward.WardId}'.");
        }

        public void DebugTriggerWardShadow(AnimalHospitalWard ward = null)
        {
            var cfg = _service.Config;
            if (cfg == null)
                return;

            ward ??= _service.PickRandomOccupiedOrAnyWard();
            if (ward == null)
            {
                Debug.LogWarning("[AnimalHospital] DebugTriggerWardShadow: no ward available.");
                return;
            }

            if (HasWardShadow && _wardShadow != null)
            {
                Object.Destroy(_wardShadow.gameObject);
                _wardShadow = null;
            }

            _wardShadow = SpawnShadowAt(ward.ShadowSpawnPoint.position, cfg, fromWard: true);
            Debug.Log($"[AnimalHospital] Debug: ward shadow spawned on ward '{ward.WardId}'.");
        }

        public void DebugTriggerWardFire(AnimalHospitalWard ward = null)
        {
            if (HasWardFire)
                ClearWardFire();

            ward ??= _service.PickRandomOccupiedOrAnyWard();
            if (ward == null)
            {
                Debug.LogWarning("[AnimalHospital] DebugTriggerWardFire: no ward available.");
                return;
            }

            _burningWard = ward;
            _burningElapsed = 0f;
            ward.SetOnFire(true);
            var patient = ward.Occupant;
            if (patient != null)
                patient.SetOnFire(true);

            Debug.Log($"[AnimalHospital] Debug: ward fire started on '{ward.WardId}'.");
        }

        public void DebugTriggerCorridorSlime()
        {
            var cfg = _service.Config;
            if (cfg == null)
                return;

            if (HasCorridorSlime && _slime != null)
            {
                Object.Destroy(_slime.gameObject);
                _slime = null;
            }

            TryTriggerCorridorSlime(cfg);
            Debug.Log("[AnimalHospital] Debug: corridor slime trigger attempted.");
        }

        public void DebugTriggerCorridorShadow()
        {
            var cfg = _service.Config;
            if (cfg == null)
                return;

            if (HasCorridorShadow && _corridorShadow != null)
            {
                Object.Destroy(_corridorShadow.gameObject);
                _corridorShadow = null;
            }

            TryTriggerCorridorShadow(cfg);
            Debug.Log("[AnimalHospital] Debug: corridor shadow trigger attempted.");
        }
#endif

        void TryTriggerWardFire(Config.AnimalHospitalConfig cfg)
        {
            if (HasWardFire)
                return;

            var ward = _service.PickRandomOccupiedOrAnyWard();
            if (ward == null)
                return;

            _burningWard = ward;
            _burningElapsed = 0f;
            ward.SetOnFire(true);

            var patient = ward.Occupant;
            if (patient != null)
                patient.SetOnFire(true);
        }

        void TryTriggerCorridorSlime(Config.AnimalHospitalConfig cfg)
        {
            if (HasCorridorSlime)
                return;

            var corridor = _service.CorridorZone;
            if (corridor == null)
                return;

            var prefab = cfg.slimePrefab;
            var go = prefab != null
                ? Object.Instantiate(prefab, corridor.GetRandomPoint(), Quaternion.identity)
                : CreatePrimitiveMarker(corridor.GetRandomPoint(), new Color(0.2f, 0.8f, 0.2f, 0.7f), "Slime");
            _slime = go.GetComponent<AnimalHospitalSlimeHazard>() ?? go.AddComponent<AnimalHospitalSlimeHazard>();
            _slime.Initialize(_service, cfg.corridorSlimeDurationSeconds, cfg.corridorSlimeGrowSpeed);
        }

        void TryTriggerCorridorShadow(Config.AnimalHospitalConfig cfg)
        {
            if (HasCorridorShadow)
                return;

            var corridor = _service.CorridorZone;
            if (corridor == null)
                return;

            _corridorShadow = SpawnShadowAt(corridor.GetRandomPoint(), cfg, fromWard: false);
        }

        void SpawnCeiling(AnimalHospitalWard ward, Config.AnimalHospitalConfig cfg)
        {
            var prefab = cfg.ceilingObjectPrefab;
            var pos = ward.CeilingEventAnchor.position;
            var go = prefab != null
                ? Object.Instantiate(prefab, pos, Quaternion.identity, ward.transform)
                : CreatePrimitiveMarker(pos, Color.red, "CeilingHazard");
            _ceiling = go.GetComponent<AnimalHospitalCeilingHazard>()
                       ?? go.AddComponent<AnimalHospitalCeilingHazard>();
            _ceiling.Initialize(_service, ward);
        }

        void SpawnWardShadow(AnimalHospitalWard ward, Config.AnimalHospitalConfig cfg)
        {
            _wardShadow = SpawnShadowAt(ward.ShadowSpawnPoint.position, cfg, fromWard: true);
        }

        AnimalHospitalShadowAi SpawnShadowAt(Vector3 position, Config.AnimalHospitalConfig cfg, bool fromWard)
        {
            var prefab = cfg.shadowPrefab;
            var go = prefab != null
                ? Object.Instantiate(prefab, position, Quaternion.identity)
                : CreatePrimitiveMarker(position, Color.black, "Shadow");
            var shadow = go.GetComponent<AnimalHospitalShadowAi>() ?? go.AddComponent<AnimalHospitalShadowAi>();
            shadow.Initialize(_service, cfg.shadowMaxHealth, fromWard);
            return shadow;
        }

        void TickWardFire(float dt, Config.AnimalHospitalConfig cfg)
        {
            if (_burningWard == null)
                return;

            _burningElapsed += dt;
            if (_burningElapsed >= cfg.wardFireDurationSeconds)
                ClearWardFire();
        }

        void TickPlayerInBurningWard(float dt, Config.AnimalHospitalConfig cfg)
        {
            if (_burningWard == null)
                return;

            var player = _service.PlayerTransform;
            if (player == null)
                return;

            if (!_service.IsPlayerInsideWard(_burningWard, player.position))
                return;

            _proxy.FireSanityAccum += dt;
            if (_proxy.FireSanityAccum >= 1f)
            {
                _proxy.FireSanityAccum = 0f;
                _service.Sanity.ApplyDamage(cfg.wardFireSanityLossPerSecond);
            }
        }

        void ClearWardFire()
        {
            if (_burningWard != null)
            {
                _burningWard.SetOnFire(false);
                if (_burningWard.Occupant != null)
                    _burningWard.Occupant.SetOnFire(false);
            }

            _burningWard = null;
            _burningElapsed = 0f;
        }

        static GameObject CreatePrimitiveMarker(Vector3 position, Color color, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.6f;
            var r = go.GetComponent<Renderer>();
            if (r != null)
                r.material.color = color;
            return go;
        }

        sealed class AnimalHospitalConfigProxy
        {
            public float FireSanityAccum;
            public AnimalHospitalConfigProxy(AnimalHospitalService _) { }
        }
    }
}
