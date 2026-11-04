using Game.Audio;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 动物医院模式音效（本模式自管多路循环，不依赖 GameBootstrap 的 SFX 表）。
    /// 缺少 AudioManager 时仍会播本地音量；有 AudioManager 则跟随用户 SFX 音量。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalAudio : MonoBehaviour
    {
        public static AnimalHospitalAudio Instance { get; private set; }

        [Header("BGM")]
        [SerializeField] private AudioClip bgm;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.55f;

        [Header("One-shot")]
        [SerializeField] private AudioClip interactComplete;
        [SerializeField] private AudioClip pistolShot;
        [SerializeField] private AudioClip cameraShutter;
        [SerializeField] private AudioClip batonSwing;
        [SerializeField] private AudioClip[] patientHurtClips;

        [Header("Loops")]
        [SerializeField] private AudioClip freezeGunLoop;
        [SerializeField] private AudioClip extinguisherLoop;
        [SerializeField] private AudioClip taserLoop;
        [SerializeField] private AudioClip computerAnalysisLoop;
        [SerializeField] private AudioClip wardFireLoop;
        [SerializeField] private AudioClip receptionWaitLoop;
        [SerializeField] private AudioClip lowSanityHeartbeatLoop;

        AudioSource _oneshot;
        AudioSource _bgm;
        AudioSource _weaponLoop;
        AudioSource _computerLoop;
        AudioSource _fireLoop;
        AudioSource _receptionLoop;
        AudioSource _heartbeatLoop;

        AnimalHospitalPropId _weaponLoopProp = AnimalHospitalPropId.None;
        bool _gameplayAudioActive;

        public static AnimalHospitalAudio Resolve()
        {
            if (Instance != null)
                return Instance;
            return FindObjectOfType<AnimalHospitalAudio>();
        }

        void Awake()
        {
            Instance = this;
            EnsureSources();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            AudioManager.UserSfxVolumeChanged += OnUserSfxVolumeChanged;
        }

        void OnDisable()
        {
            AudioManager.UserSfxVolumeChanged -= OnUserSfxVolumeChanged;
            StopAllLoops();
            StopBgm();
        }

        public void BeginGameplayAudio()
        {
            _gameplayAudioActive = true;
            EnsureSources();
            PlayBgm();
        }

        public void EndGameplayAudio()
        {
            _gameplayAudioActive = false;
            StopAllLoops();
            StopBgm();
        }

        public void PlayInteractComplete() => PlayOneShot(interactComplete);
        public void PlayPistolShot() => PlayOneShot(pistolShot);
        public void PlayCameraShutter() => PlayOneShot(cameraShutter);
        public void PlayBatonSwing() => PlayOneShot(batonSwing);

        public void PlayPatientHurt()
        {
            if (patientHurtClips == null || patientHurtClips.Length == 0)
                return;
            var clip = patientHurtClips[Random.Range(0, patientHurtClips.Length)];
            PlayOneShot(clip);
        }

        public void SetWeaponAttackLoop(AnimalHospitalPropId prop, bool active)
        {
            if (!active || prop == AnimalHospitalPropId.None)
            {
                StopWeaponLoop();
                return;
            }

            AudioClip clip = prop switch
            {
                AnimalHospitalPropId.FreezeGun => freezeGunLoop,
                AnimalHospitalPropId.FireExtinguisher => extinguisherLoop,
                AnimalHospitalPropId.Taser => taserLoop,
                _ => null,
            };

            if (clip == null)
            {
                StopWeaponLoop();
                return;
            }

            if (_weaponLoop != null && _weaponLoop.isPlaying && _weaponLoopProp == prop && _weaponLoop.clip == clip)
                return;

            EnsureSources();
            _weaponLoopProp = prop;
            _weaponLoop.clip = clip;
            _weaponLoop.loop = true;
            _weaponLoop.volume = SfxVolume;
            _weaponLoop.Play();
        }

        public void StopWeaponLoop()
        {
            _weaponLoopProp = AnimalHospitalPropId.None;
            if (_weaponLoop != null && _weaponLoop.isPlaying)
                _weaponLoop.Stop();
        }

        void SetLoop(AudioSource source, AudioClip clip, bool active)
        {
            EnsureSources();
            if (source == null)
                return;

            if (!active || clip == null)
            {
                if (source.isPlaying)
                    source.Stop();
                return;
            }

            if (source.isPlaying && source.clip == clip)
            {
                source.volume = SfxVolume;
                return;
            }

            source.clip = clip;
            source.loop = true;
            source.volume = SfxVolume;
            source.Play();
        }

        public void SetComputerAnalysisLoop(bool active)
        {
            EnsureSources();
            SetLoop(_computerLoop, computerAnalysisLoop, active);
        }

        public void SetWardFireLoop(bool active)
        {
            EnsureSources();
            SetLoop(_fireLoop, wardFireLoop, active);
        }

        public void SetReceptionWaitLoop(bool active)
        {
            EnsureSources();
            SetLoop(_receptionLoop, receptionWaitLoop, active);
        }

        public void SetLowSanityHeartbeat(bool active)
        {
            EnsureSources();
            SetLoop(_heartbeatLoop, lowSanityHeartbeatLoop, active);
        }

        void PlayBgm()
        {
            if (bgm == null)
                return;
            EnsureSources();
            _bgm.clip = bgm;
            _bgm.loop = true;
            _bgm.volume = Mathf.Clamp01(bgmVolume * BgmVolume);
            if (!_bgm.isPlaying)
                _bgm.Play();
        }

        void StopBgm()
        {
            if (_bgm != null && _bgm.isPlaying)
                _bgm.Stop();
        }

        void StopAllLoops()
        {
            StopWeaponLoop();
            SetComputerAnalysisLoop(false);
            SetWardFireLoop(false);
            SetReceptionWaitLoop(false);
            SetLowSanityHeartbeat(false);
        }

        void PlayOneShot(AudioClip clip)
        {
            if (clip == null)
                return;
            EnsureSources();
            _oneshot.PlayOneShot(clip, SfxVolume);
        }

        void EnsureSources()
        {
            _oneshot ??= CreateLoopSource("AH_OneShot");
            _oneshot.loop = false;
            _bgm ??= CreateLoopSource("AH_Bgm");
            _weaponLoop ??= CreateLoopSource("AH_WeaponLoop");
            _computerLoop ??= CreateLoopSource("AH_ComputerLoop");
            _fireLoop ??= CreateLoopSource("AH_FireLoop");
            _receptionLoop ??= CreateLoopSource("AH_ReceptionWaitLoop");
            _heartbeatLoop ??= CreateLoopSource("AH_HeartbeatLoop");
        }

        AudioSource CreateLoopSource(string childName)
        {
            var existing = transform.Find(childName);
            AudioSource src;
            if (existing != null)
            {
                src = existing.GetComponent<AudioSource>() ?? existing.gameObject.AddComponent<AudioSource>();
            }
            else
            {
                var go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                src = go.AddComponent<AudioSource>();
            }

            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = true;
            return src;
        }

        void OnUserSfxVolumeChanged(float _)
        {
            RefreshLoopVolumes();
        }

        void RefreshLoopVolumes()
        {
            var sfx = SfxVolume;
            if (_weaponLoop != null && _weaponLoop.isPlaying)
                _weaponLoop.volume = sfx;
            if (_computerLoop != null && _computerLoop.isPlaying)
                _computerLoop.volume = sfx;
            if (_fireLoop != null && _fireLoop.isPlaying)
                _fireLoop.volume = sfx;
            if (_receptionLoop != null && _receptionLoop.isPlaying)
                _receptionLoop.volume = sfx;
            if (_heartbeatLoop != null && _heartbeatLoop.isPlaying)
                _heartbeatLoop.volume = sfx;
            if (_bgm != null && _bgm.isPlaying)
                _bgm.volume = Mathf.Clamp01(bgmVolume * BgmVolume);
        }

        float SfxVolume
        {
            get
            {
                var am = AudioManager.Instance;
                return am != null ? Mathf.Clamp01(am.UserSfxVolume01) : 1f;
            }
        }

        float BgmVolume
        {
            get
            {
                var am = AudioManager.Instance;
                return am != null ? Mathf.Clamp01(am.UserBgmVolume01) : 1f;
            }
        }
    }
}
