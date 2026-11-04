using Game.Audio;
using UnityEngine;

namespace Game.EscapeSnake
{
    /// <summary>
    /// 多节虫循环音效：移动循环 + 处于 BuildingWall 层墙体内循环。
    /// Clip 在 <see cref="Game.Core.GameBootstrapper"/> 的 bootstrapSfxLibrary 中绑定
    /// <see cref="SfxId.WormMoveLoop"/> / <see cref="SfxId.WormInsideWallLoop"/>（须勾选 Loop）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WormChainMotor))]
    public sealed class WormChainMotorAudio : MonoBehaviour
    {
        static readonly Collider[] WallHitBuffer = new Collider[12];

        [SerializeField] private WormChainMotor motor;
        [SerializeField] private SfxId moveLoopSfx = SfxId.WormMoveLoop;
        [SerializeField] private SfxId insideWallLoopSfx = SfxId.WormInsideWallLoop;
        [SerializeField] private AudioClip moveLoopClipFallback;
        [SerializeField] private AudioClip insideWallLoopClipFallback;
        [SerializeField] private string buildingWallLayerName = "BuildingWall";
        [SerializeField] private float wallProbeRadius = 0.42f;
        [Tooltip("3D 音效：全音量距离（米）。")]
        [SerializeField] private float spatialMinDistance = 3f;
        [Tooltip("3D 音效：超过此距离后迅速衰减至无声（米）。")]
        [SerializeField] private float spatialMaxDistance = 12f;

        AudioSource _moveLoopSource;
        AudioSource _wallLoopSource;
        int _buildingWallMask;
        bool _moveLoopActive;
        bool _wallLoopActive;

        void OnEnable()
        {
            AudioManager.UserSfxVolumeChanged += OnUserSfxVolumeChanged;
        }

        void OnDisable()
        {
            AudioManager.UserSfxVolumeChanged -= OnUserSfxVolumeChanged;
            StopLoop(_moveLoopSource, ref _moveLoopActive);
            StopLoop(_wallLoopSource, ref _wallLoopActive);
        }

        void OnUserSfxVolumeChanged(float _)
        {
            if (_moveLoopActive && _moveLoopSource != null)
                ApplyUserVolume(_moveLoopSource, ResolveVolumeScale(moveLoopSfx, moveLoopClipFallback));
            if (_wallLoopActive && _wallLoopSource != null)
                ApplyUserVolume(_wallLoopSource, ResolveVolumeScale(insideWallLoopSfx, insideWallLoopClipFallback));
        }

        float ResolveVolumeScale(SfxId sfxId, AudioClip fallbackClip)
        {
            var am = AudioManager.Instance;
            if (am != null && am.TryGetSfxClip(sfxId, out _, out var scale))
                return scale;

            return 1f;
        }

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<WormChainMotor>();

            var am = AudioManager.EnsureCreated();
            if (am != null)
            {
                if (moveLoopClipFallback != null)
                    am.EnsureSfx(moveLoopSfx, moveLoopClipFallback);
                if (insideWallLoopClipFallback != null)
                    am.EnsureSfx(insideWallLoopSfx, insideWallLoopClipFallback);
            }

            _buildingWallMask = LayerMask.GetMask(buildingWallLayerName);
            _moveLoopSource = CreateLoopSource("WormMoveLoopAudio");
            _wallLoopSource = CreateLoopSource("WormInsideWallLoopAudio");
        }

        void Start()
        {
            TryBindFallbackClipsFromManager();
        }

        void TryBindFallbackClipsFromManager()
        {
            var am = AudioManager.Instance;
            if (am == null)
                return;

            if (moveLoopClipFallback == null &&
                am.TryGetSfxClip(moveLoopSfx, out var moveClip, out _) &&
                moveClip != null)
            {
                moveLoopClipFallback = moveClip;
            }

            if (insideWallLoopClipFallback == null &&
                am.TryGetSfxClip(insideWallLoopSfx, out var wallClip, out _) &&
                wallClip != null)
            {
                insideWallLoopClipFallback = wallClip;
            }
        }

        void Update()
        {
            if (motor == null)
                return;

            var shouldMoveLoop = motor.IsPlaying;
            var shouldWallLoop = shouldMoveLoop && IsWormInsideBuildingWall();
            UpdateLoop(_moveLoopSource, moveLoopSfx, shouldMoveLoop, ref _moveLoopActive);
            UpdateLoop(_wallLoopSource, insideWallLoopSfx, shouldWallLoop, ref _wallLoopActive);

            var follow = motor.HeadTransform != null ? motor.HeadTransform.position : transform.position;
            if (_moveLoopSource != null)
                _moveLoopSource.transform.position = follow;
            if (_wallLoopSource != null)
                _wallLoopSource.transform.position = follow;
        }

        bool IsWormInsideBuildingWall()
        {
            if (_buildingWallMask == 0)
                return false;

            if (motor.HeadTransform != null &&
                IsPointInsideBuildingWall(motor.HeadTransform.position))
                return true;

            var segments = motor.SegmentCount;
            if (segments <= 1)
                return false;

            var step = Mathf.Max(1, segments / 4);
            for (var i = step; i < segments; i += step)
            {
                var seg = motor.GetSegmentTransform(i);
                if (seg != null && IsPointInsideBuildingWall(seg.position))
                    return true;
            }

            return false;
        }

        bool IsPointInsideBuildingWall(Vector3 worldPos)
        {
            var count = Physics.OverlapSphereNonAlloc(
                worldPos,
                wallProbeRadius,
                WallHitBuffer,
                _buildingWallMask,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < count; i++)
            {
                var col = WallHitBuffer[i];
                if (col == null)
                    continue;

                if (IsPointInsideCollider(col, worldPos))
                    return true;
            }

            return false;
        }

        static bool IsPointInsideCollider(Collider col, Vector3 worldPos)
        {
            var closest = col.ClosestPoint(worldPos);
            return (closest - worldPos).sqrMagnitude < 0.0004f;
        }

        void UpdateLoop(AudioSource source, SfxId sfxId, bool shouldPlay, ref bool activeFlag)
        {
            if (source == null)
                return;

            if (!shouldPlay)
            {
                StopLoop(source, ref activeFlag);
                return;
            }

            if (!activeFlag || source.clip == null)
                StartLoop(source, sfxId, ref activeFlag);
            else
                ApplyUserVolume(source);
        }

        void StartLoop(AudioSource source, SfxId sfxId, ref bool activeFlag)
        {
            var am = AudioManager.Instance;
            AudioClip clip = null;
            var volumeScale = 1f;
            if (am != null && am.TryGetSfxClip(sfxId, out clip, out volumeScale) && clip != null)
            {
                // resolved from bootstrap library
            }
            else if (sfxId == moveLoopSfx && moveLoopClipFallback != null)
            {
                clip = moveLoopClipFallback;
            }
            else if (sfxId == insideWallLoopSfx && insideWallLoopClipFallback != null)
            {
                clip = insideWallLoopClipFallback;
            }

            if (clip == null)
            {
                StopLoop(source, ref activeFlag);
                return;
            }

            source.clip = clip;
            source.loop = true;
            ApplyUserVolume(source, volumeScale);
            if (!source.isPlaying)
                source.Play();
            activeFlag = true;
        }

        void ApplyUserVolume(AudioSource source, float volumeScale = 1f)
        {
            var user = AudioManager.Instance != null ? AudioManager.Instance.UserSfxVolume01 : 1f;
            source.volume = Mathf.Clamp01(volumeScale * user);
        }

        static void StopLoop(AudioSource source, ref bool activeFlag)
        {
            activeFlag = false;
            if (source != null && source.isPlaying)
                source.Stop();
        }

        AudioSource CreateLoopSource(string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            AudioManager.Instance?.ConfigureLoopSource(source);
            ApplySpatialSettings(source);
            return source;
        }

        void ApplySpatialSettings(AudioSource source)
        {
            if (source == null)
                return;

            source.spatialBlend = 1f;
            source.minDistance = Mathf.Max(0.5f, spatialMinDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 1f, spatialMaxDistance);
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0f;
            source.spread = 60f;
        }
    }
}
