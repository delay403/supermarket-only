using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.DestroyCity
{
    public enum NukeLaunchViewMode
    {
        Player = 0,
        Missile = 1,
    }

    /// <summary>
    /// 导弹发射期间在「玩家视角/操控」与「导弹视角/落点操控」之间切换。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NukeLaunchViewModeController : MonoBehaviour
    {
        public static NukeLaunchViewModeController Instance { get; private set; }

        [SerializeField] private NukeMissileFlightCamera flightCamera;

        NukeLauncherController _launcher;
        LegacyPlayerInput _playerInput;
        PlayerMotor _playerMotor;
        WorldRaycastInteractor _worldInteractor;
        ThirdPersonCamera _thirdPersonCamera;
        Transform _missile;
        Transform _cityTarget;

        bool _sessionActive;
        bool _explosionWatchPhase;
        NukeLaunchViewMode _mode = NukeLaunchViewMode.Player;

        public bool IsSessionActive => _sessionActive || _explosionWatchPhase;
        public NukeLaunchViewMode Mode => _mode;
        public bool IsMissileMode =>
            (_sessionActive || _explosionWatchPhase) && _mode == NukeLaunchViewMode.Missile;

        public static event System.Action<bool> SessionActiveChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (flightCamera == null)
                flightCamera = FindObjectOfType<NukeMissileFlightCamera>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void BeginLaunchSession(
            NukeLauncherController launcher,
            Transform missile,
            Transform cityTarget)
        {
            _launcher = launcher;
            _missile = missile;
            _cityTarget = cityTarget;
            _sessionActive = missile != null;
            _explosionWatchPhase = false;
            _mode = NukeLaunchViewMode.Missile;

            ResolvePlayerRefs();
            flightCamera?.SetFollowTargets(missile, cityTarget);
            ApplyMode();
            NotifySessionActiveChanged();
        }

        public void UpdateMissileTransform(Transform missile)
        {
            _missile = missile;
            flightCamera?.SetFollowTargets(missile, _cityTarget);
        }

        public void BeginExplosionWatchPhase()
        {
            _explosionWatchPhase = true;
        }

        public void SetMode(NukeLaunchViewMode mode)
        {
            if (!IsSessionActive)
                return;

            _mode = mode;

            if (mode == NukeLaunchViewMode.Player
                && _explosionWatchPhase
                && flightCamera != null
                && flightCamera.IsWatchingExplosion)
            {
                flightCamera.EndExplosionWatch();
            }

            ApplyMode();
        }

        public void ForcePlayerModeAfterExplosion()
        {
            _mode = NukeLaunchViewMode.Player;
            flightCamera?.EndExplosionWatch();
            ApplyMode();
        }

        public void EndLaunchSession()
        {
            _sessionActive = false;
            _explosionWatchPhase = false;
            _launcher = null;
            _missile = null;
            _cityTarget = null;
            _mode = NukeLaunchViewMode.Player;
            ApplyMode();
            flightCamera?.SetFollowTargets(null, null);
            NotifySessionActiveChanged();
        }

        static void NotifySessionActiveChanged()
        {
            var active = Instance != null && Instance.IsSessionActive;
            SessionActiveChanged?.Invoke(active);
        }

        public void ToggleMode()
        {
            if (!IsSessionActive)
                return;

            SetMode(_mode == NukeLaunchViewMode.Player
                ? NukeLaunchViewMode.Missile
                : NukeLaunchViewMode.Player);
        }

        public void OnDescentSteeringChanged(bool active)
        {
            if (!_sessionActive || _mode != NukeLaunchViewMode.Missile)
                return;

            ApplyMissileInput(active);
        }

        void ResolvePlayerRefs()
        {
            _playerInput ??= FindObjectOfType<LegacyPlayerInput>();
            _playerMotor ??= FindObjectOfType<PlayerMotor>();
            _worldInteractor ??= FindObjectOfType<WorldRaycastInteractor>();
            _thirdPersonCamera ??= FindObjectOfType<ThirdPersonCamera>();
            if (flightCamera == null)
                flightCamera = FindObjectOfType<NukeMissileFlightCamera>();
        }

        void ApplyMode()
        {
            if (!IsSessionActive || _mode == NukeLaunchViewMode.Player)
                ApplyPlayerMode();
            else
                ApplyMissileMode();
        }

        void ApplyPlayerMode()
        {
            if (flightCamera == null
                || !_explosionWatchPhase
                || !flightCamera.IsWatchingExplosion)
            {
                flightCamera?.SetMissileFollowActive(false);
            }

            EnableThirdPersonCamera();

            if (_playerMotor != null)
                _playerMotor.enabled = true;

            if (_worldInteractor != null)
                _worldInteractor.enabled = true;

            _playerInput?.SetLocked(false);
        }

        void ApplyMissileMode()
        {
            if (_explosionWatchPhase)
                return;

            if (_playerMotor != null)
                _playerMotor.enabled = false;

            if (_worldInteractor != null)
                _worldInteractor.enabled = false;

            flightCamera?.SetMissileFollowActive(true);
            ApplyMissileInput(_launcher != null && _launcher.IsDescentSteeringActive);
        }

        void EnableThirdPersonCamera()
        {
            ResolvePlayerRefs();
            if (_thirdPersonCamera != null)
                _thirdPersonCamera.enabled = true;
        }

        void ApplyMissileInput(bool allowSteerMove)
        {
            _playerInput?.SetLocked(true, allowLookWhileLocked: false, allowMoveWhileLocked: allowSteerMove);
        }
    }
}
