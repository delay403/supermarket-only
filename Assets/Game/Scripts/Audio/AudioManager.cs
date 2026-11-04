using System;
using System.Collections.Generic;
using Game.Platform;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Game.Save;
using FairyGUI;

namespace Game.Audio
{
    /// <summary>
    /// 全局音频：一条 BGM + 若干 pooled <see cref="AudioSource"/> 播 OneShot。
    /// 可选挂载 <see cref="AudioMixerGroup"/> 做分类路由；整体混音可走 <see cref="AudioMixer"/> 暴露参数。
    /// 循环风声见 <see cref="ReportAmbientWindDrive01"/>，使用 <see cref="SfxId.AmbientWind"/> 的 Clip（须设为 Loop）。
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class AudioManager : MonoBehaviour
    {
        [Serializable]
        public struct SfxClipEntry
        {
            public SfxId id;
            public AudioClip clip;
            [Range(0f, 2f)] public float volumeScale;
        }

        public static AudioManager Instance { get; private set; }

        /// <summary>用户 SFX 线性音量变更（供外部循环音源刷新音量）。</summary>
        public static event Action<float> UserSfxVolumeChanged;

        [Header("Mixer (optional)")]
        [SerializeField] private AudioMixer mainMixer;
        [SerializeField] private string exposedParamMasterDb = string.Empty;
        [SerializeField] private string exposedParamBgmDb = string.Empty;
        [SerializeField] private string exposedParamSfxDb = string.Empty;

        [Header("Outputs")]
        [SerializeField] private AudioMixerGroup bgmOutputGroup;
        [SerializeField] private AudioMixerGroup sfxOutputGroup;

        [Header("BGM")]
        [SerializeField] private AudioClip defaultBgmClip;
        [SerializeField] [Range(0f, 1f)] private float bgmVolumeScale = 1f;
        [SerializeField] private bool playBgmOnStart = true;

        [Header("SFX pool")]
        [SerializeField] private int sfxVoiceCount = 14;
        [Tooltip("同一种 SfxId 同时播放的上限。")]
        [SerializeField] [Min(1)] private int maxConcurrentPerSfxId = 8;
        [SerializeField] private List<SfxClipEntry> sfxLibrary = new List<SfxClipEntry>();

        [Header("Fishing reel press")]
        [Tooltip("两次抽杆音效之间的最短间隔（秒），避免连点叠音。")]
        [SerializeField] private float fishingReelPressMinIntervalSeconds = 1f;

        [Header("Ambient wind (SfxId.AmbientWind, looping clip)")]
        [SerializeField] private float windPitchMin = 0.92f;
        [SerializeField] private float windPitchMax = 1.12f;
        [SerializeField] private float windRiseSpeed = 8f;
        [SerializeField] private float windFallSpeed = 3f;

        private readonly Dictionary<SfxId, SfxClipEntry> _map = new Dictionary<SfxId, SfxClipEntry>();
        private readonly Dictionary<SfxId, int> _sfxActiveCounts = new Dictionary<SfxId, int>();
        private AudioSource _bgmSource;
        private AudioSource[] _sfxPool;
        private int _sfxCursor;

        private float _userSfxLinear01 = 1f;
        private float _userBgmLinear01 = 1f;

        private AudioSource _windLoopSource;
        private float _windDriveReportedPeak;
        private bool _windDriveReported;
        private float _windSmoothedDrive01;

        static readonly SfxId[] FishingReelPressVariants =
        {
            SfxId.FishingReelPress1,
            SfxId.FishingReelPress2,
            SfxId.FishingReelPress3,
        };

        AudioSource _fishingReelPressSource;
        AudioSource _loopingSfxSource;
        SfxId _loopingSfxId = SfxId.None;
        float _lastFishingReelPressUnscaledTime = -999f;
        AudioListener _persistentListener;

        float EffectiveBgmVolume => Mathf.Clamp01(bgmVolumeScale * _userBgmLinear01);

        /// <summary>
        /// 每帧由攀爬 / 滑翔逻辑调用一次或多次；取本帧最大值。drive01∈[0,1]：0≈静默(仍将混到音量下限 0.01)，1≈满风。
        /// </summary>
        public void ReportAmbientWindDrive01(float drive01)
        {
            drive01 = Mathf.Clamp01(drive01);
            _windDriveReported = true;
            if (drive01 > _windDriveReportedPeak)
                _windDriveReportedPeak = drive01;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsurePersistentAudioListener();
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;

            PlatformMediaMgr.UserBgmVolume01Changed += OnPlatformBgmVolumeChanged;
            PlatformMediaMgr.UserSfxVolume01Changed += OnPlatformSfxVolumeChanged;

            RebuildLookup();

            WarmBgmChild();
            WarmSfxVoices(Mathf.Clamp(sfxVoiceCount, 4, 64));
            ApplyUserVolumeFromSave();
            if (playBgmOnStart && defaultBgmClip != null)
                PlayBgm(defaultBgmClip, loop: true, fadeSeconds: 0f);
        }

        /// <summary>从 <see cref="SaveService.Data"/> 应用 SFX/BGM 线性音量（设置面板与启动时调用）。</summary>
        public void ApplyUserVolumeFromSave()
        {
            if (SaveService.Data == null)
                return;
            var sfx = Mathf.Clamp01(SaveService.Data.settingsSfxVolume01);
            var bgm = Mathf.Clamp01(SaveService.Data.settingsBgmVolume01);
            RawSetUserSfxVolume01(sfx, notify: false);
            RawSetUserBgmVolume01(bgm, notify: false);
            PushSfxVolumeToPlatformMedia(sfx);
            PushBgmVolumeToPlatformMedia(bgm);
            UserSfxVolumeChanged?.Invoke(_userSfxLinear01);
        }

        static void PushSfxVolumeToPlatformMedia(float sfx)
        {
            PlayerPrefs.SetFloat(PlatformMediaMgr.SfxVolumeKey, sfx);
            PlayerPrefs.Save();
        }

        static void PushBgmVolumeToPlatformMedia(float bgm)
        {
            PlayerPrefs.SetFloat(PlatformMediaMgr.BgmVolumeKey, bgm);
            PlayerPrefs.Save();
        }

        /// <summary>用户 SFX 线性比例 [0,1]（在 clip volumeScale 之上再乘一次）。</summary>
        public float UserSfxVolume01 => _userSfxLinear01;

        /// <summary>供玩法侧自建循环音源时读取 Bootstrap 绑定的 Clip。</summary>
        public bool TryGetSfxClip(SfxId id, out AudioClip clip, out float volumeScale)
        {
            clip = null;
            volumeScale = 1f;
            if (id == SfxId.None || !_map.TryGetValue(id, out var entry) || entry.clip == null)
                return false;

            clip = entry.clip;
            volumeScale = entry.volumeScale;
            return true;
        }

        /// <summary>若库中尚无该 Sfx，则注册（不覆盖已有绑定）。</summary>
        public void EnsureSfx(SfxId id, AudioClip clip, float volumeScale = 1f)
        {
            if (id == SfxId.None || clip == null)
                return;

            if (_map.TryGetValue(id, out var existing) && existing.clip != null)
                return;

            var entry = new SfxClipEntry { id = id, clip = clip, volumeScale = volumeScale };
            sfxLibrary.Add(entry);
            _map[id] = entry;
        }

        public void ConfigureLoopSource(AudioSource source)
        {
            if (source == null)
                return;

            source.playOnAwake = false;
            source.loop = true;
            if (sfxOutputGroup != null)
                source.outputAudioMixerGroup = sfxOutputGroup;
        }

        /// <summary>用户 BGM 线性比例 [0,1]（在 bgmVolumeScale 之上再乘一次）。</summary>
        public float UserBgmVolume01 => _userBgmLinear01;

        public void SetUserSfxVolume01(float linear01, bool persist = true)
        {
            var v = Mathf.Clamp01(linear01);
            RawSetUserSfxVolume01(v);
            PushSfxVolumeToPlatformMedia(v);
            if (persist && SaveService.Data != null)
            {
                SaveService.Data.settingsSfxVolume01 = _userSfxLinear01;
                SaveService.Save();
            }
        }

        public void SetUserBgmVolume01(float linear01, bool persist = true)
        {
            var v = Mathf.Clamp01(linear01);
            RawSetUserBgmVolume01(v);
            PushBgmVolumeToPlatformMedia(v);
            if (persist && SaveService.Data != null)
            {
                SaveService.Data.settingsBgmVolume01 = _userBgmLinear01;
                SaveService.Save();
            }
        }

        void RawSetUserSfxVolume01(float v, bool notify = true)
        {
            _userSfxLinear01 = v;
            ApplySfxVolumeOnly();
            if (notify)
                UserSfxVolumeChanged?.Invoke(_userSfxLinear01);
        }

        void RawSetUserBgmVolume01(float v, bool notify = true)
        {
            _userBgmLinear01 = v;
            ApplyBgmVolumeOnly();
        }

        void ApplyBgmVolumeOnly()
        {
            if (_bgmSource != null)
                _bgmSource.volume = EffectiveBgmVolume;

            if (mainMixer != null && !string.IsNullOrEmpty(exposedParamBgmDb))
                mainMixer.SetFloat(exposedParamBgmDb, Linear01ToDb(_userBgmLinear01));
        }

        void ApplySfxVolumeOnly()
        {
            if (mainMixer != null && !string.IsNullOrEmpty(exposedParamSfxDb))
                mainMixer.SetFloat(exposedParamSfxDb, Linear01ToDb(_userSfxLinear01));

            RefreshActiveSfxSourceVolumes();
            ApplyFguiSfxVolume();
        }

        static float Linear01ToDb(float linear01)
        {
            if (linear01 <= 0.0001f)
                return -80f;
            return Mathf.Log10(Mathf.Clamp01(linear01)) * 20f;
        }

        void ApplyFguiSfxVolume()
        {
            if (Stage.inst == null)
                return;

            Stage.inst.soundVolume = _userSfxLinear01;
        }

        void RefreshActiveSfxSourceVolumes()
        {
            if (_fishingReelPressSource != null && _fishingReelPressSource.isPlaying)
            {
                var clip = _fishingReelPressSource.clip;
                var scale = ResolveClipVolumeScale(clip, 1f);
                _fishingReelPressSource.volume = Mathf.Clamp01(scale * _userSfxLinear01);
            }

            if (_loopingSfxSource != null && _loopingSfxSource.isPlaying && _loopingSfxId != SfxId.None)
            {
                if (_map.TryGetValue(_loopingSfxId, out var entry) && entry.clip != null)
                {
                    _loopingSfxSource.volume = Mathf.Clamp01(entry.volumeScale * _userSfxLinear01);
                }
            }

            if (_windLoopSource != null && _windLoopSource.isPlaying
                && _map.TryGetValue(SfxId.AmbientWind, out var windEntry) && windEntry.clip != null)
            {
                var t = Mathf.Clamp01(_windSmoothedDrive01);
                var linearVol = Mathf.Lerp(0.01f, 1f, t) * Mathf.Clamp01(windEntry.volumeScale) * _userSfxLinear01;
                _windLoopSource.volume = linearVol;
            }
        }

        float ResolveClipVolumeScale(AudioClip clip, float fallback)
        {
            if (clip == null)
                return fallback;

            foreach (var pair in _map)
            {
                if (pair.Value.clip == clip)
                    return pair.Value.volumeScale;
            }

            return fallback;
        }

        private void LateUpdate() => FlushAmbientWind();

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            PlatformMediaMgr.UserBgmVolume01Changed -= OnPlatformBgmVolumeChanged;
            PlatformMediaMgr.UserSfxVolume01Changed -= OnPlatformSfxVolumeChanged;
            if (Instance == this)
                Instance = null;
        }

        static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance != null)
                Instance.EnforceSingleAudioListener();
        }

        void EnsurePersistentAudioListener()
        {
            _persistentListener = GetComponent<AudioListener>();
            if (_persistentListener == null)
                _persistentListener = gameObject.AddComponent<AudioListener>();

            _persistentListener.enabled = true;
            EnforceSingleAudioListener();
        }

        void EnforceSingleAudioListener()
        {
            if (_persistentListener == null)
                return;

            _persistentListener.enabled = true;

#if UNITY_2023_1_OR_NEWER
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var listeners = FindObjectsOfType<AudioListener>(true);
#endif
            for (var i = 0; i < listeners.Length; i++)
            {
                var listener = listeners[i];
                if (listener == null || listener == _persistentListener)
                    continue;

                listener.enabled = false;
            }
        }

        void OnPlatformBgmVolumeChanged(float linear01) => RawSetUserBgmVolume01(linear01);

        void OnPlatformSfxVolumeChanged(float linear01) => RawSetUserSfxVolume01(linear01);

        public static AudioManager EnsureCreated()
        {
            if (Instance != null)
            {
                Instance.EnsurePersistentAudioListener();
                return Instance;
            }

            var go = new GameObject(nameof(AudioManager));
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AudioManager>();
            return Instance;
        }

        public void ReplaceSfxLibrary(IReadOnlyList<SfxClipEntry> entries)
        {
            sfxLibrary = entries != null ? new List<SfxClipEntry>(entries) : new List<SfxClipEntry>();
            RebuildLookup();
        }

        /// <summary>按 <see cref="SfxId"/> 合并条目；同 id 后者覆盖前者。</summary>
        public void MergeSfxLibrary(IReadOnlyList<SfxClipEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.clip == null || entry.id == SfxId.None)
                    continue;

                var replaced = false;
                for (var j = 0; j < sfxLibrary.Count; j++)
                {
                    if (sfxLibrary[j].id != entry.id)
                        continue;

                    sfxLibrary[j] = entry;
                    replaced = true;
                    break;
                }

                if (!replaced)
                    sfxLibrary.Add(entry);
            }

            RebuildLookup();
        }

        public static void BootstrapFromLists(IReadOnlyList<SfxClipEntry> sfx, AudioClip bgmClip, bool autoplay = true)
        {
            var mgr = EnsureCreated();
            mgr.ReplaceSfxLibrary(sfx);
            if (bgmClip != null)
                mgr.defaultBgmClip = bgmClip;
            if (autoplay && bgmClip != null)
                mgr.PlayBgm(bgmClip, loop: true, fadeSeconds: 0f);
        }

        private void RebuildLookup()
        {
            _map.Clear();
            for (var i = 0; i < sfxLibrary.Count; i++)
            {
                var e = sfxLibrary[i];
                if (e.clip == null || e.id == SfxId.None)
                    continue;
                _map[e.id] = e;
            }
        }

        private void WarmBgmChild()
        {
            if (_bgmSource != null)
                return;

            var go = new GameObject("BGM");
            go.transform.SetParent(transform, false);
            _bgmSource = go.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.outputAudioMixerGroup = bgmOutputGroup;
        }

        private void WarmSfxVoices(int count)
        {
            var rootName = "__SFXPool";
            Transform rootTf = transform.Find(rootName);
            GameObject root;
            if (rootTf == null)
            {
                root = new GameObject(rootName);
                root.transform.SetParent(transform, false);
            }
            else
                root = rootTf.gameObject;

            if (_sfxPool != null && _sfxPool.Length == count && root.transform.childCount == count)
                return;

            for (var i = root.transform.childCount - 1; i >= 0; i--)
                Destroy(root.transform.GetChild(i).gameObject);

            _sfxPool = new AudioSource[count];
            for (var i = 0; i < count; i++)
            {
                var c = new GameObject($"Sfx_{i}");
                c.transform.SetParent(root.transform, false);
                var s = c.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 0f;
                s.outputAudioMixerGroup = sfxOutputGroup;
                _sfxPool[i] = s;
            }

            _sfxCursor = 0;
        }

        public void PlayBgm(AudioClip clip, bool loop = true, float fadeSeconds = 0f)
        {
            WarmBgmChild();
            if (_bgmSource == null || clip == null)
                return;

            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            _bgmSource.volume = fadeSeconds > 0.01f ? 0f : EffectiveBgmVolume;
            _bgmSource.Play();
            if (fadeSeconds > 0.01f)
                StartCoroutine(FadeBgmVolume(EffectiveBgmVolume, fadeSeconds));
        }

        public void StopBgm()
        {
            if (_bgmSource != null)
                _bgmSource.Stop();
        }

        private System.Collections.IEnumerator FadeBgmVolume(float target, float dur)
        {
            var t = 0f;
            var start = _bgmSource != null ? _bgmSource.volume : 0f;
            while (t < dur && _bgmSource != null)
            {
                t += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / dur));
                yield return null;
            }

            if (_bgmSource != null)
                _bgmSource.volume = target;
        }

        public void PlaySfx(SfxId id, float volumeMultiplier = 1f)
        {
            if (id == SfxId.None || !_map.TryGetValue(id, out var entry) || entry.clip == null)
                return;

            if (!TryAcquireSfxSlot(id))
                return;

            WarmSfxVoices(Mathf.Clamp(sfxVoiceCount, 4, 64));
            var src = NextFreeSfxSource();
            if (src == null)
            {
                ReleaseSfxSlot(id);
                return;
            }

            var v = Mathf.Clamp01(entry.volumeScale * volumeMultiplier * _userSfxLinear01);
            src.spatialBlend = 0f;
            src.pitch = 1f;
            src.PlayOneShot(entry.clip, v);
            StartCoroutine(CoReleaseSfxSlotAfter(id, entry.clip.length));
        }

        bool TryAcquireSfxSlot(SfxId id)
        {
            var max = Mathf.Max(1, maxConcurrentPerSfxId);
            _sfxActiveCounts.TryGetValue(id, out var count);
            if (count >= max)
                return false;

            _sfxActiveCounts[id] = count + 1;
            return true;
        }

        void ReleaseSfxSlot(SfxId id)
        {
            if (!_sfxActiveCounts.TryGetValue(id, out var count))
                return;

            count = Mathf.Max(0, count - 1);
            if (count <= 0)
                _sfxActiveCounts.Remove(id);
            else
                _sfxActiveCounts[id] = count;
        }

        System.Collections.IEnumerator CoReleaseSfxSlotAfter(SfxId id, float clipLengthSeconds)
        {
            var wait = Mathf.Max(0.01f, clipLengthSeconds);
            yield return new WaitForSecondsRealtime(wait);
            ReleaseSfxSlot(id);
        }

        /// <summary>
        /// 兼容旧调用：与 <see cref="PlaySfx"/> 相同，忽略世界坐标，不做距离衰减。
        /// </summary>
        public void PlaySfx3D(SfxId id, Vector3 worldPosition, float volumeMultiplier = 1f, float minDist = 1f, float maxDist = 40f)
        {
            PlaySfx(id, volumeMultiplier);
        }

        /// <summary>
        /// 抽杆三选一：播放前先停掉上一段；<see cref="fishingReelPressMinIntervalSeconds"/> 内最多触发一次。
        /// </summary>
        public void PlayFishingReelPressRandom()
        {
            var minInterval = Mathf.Max(0f, fishingReelPressMinIntervalSeconds);
            if (Time.unscaledTime - _lastFishingReelPressUnscaledTime < minInterval)
                return;

            var pick = FishingReelPressVariants[UnityEngine.Random.Range(0, FishingReelPressVariants.Length)];
            if (!_map.TryGetValue(pick, out var entry) || entry.clip == null)
                return;

            EnsureFishingReelPressSource();
            if (_fishingReelPressSource.isPlaying)
                _fishingReelPressSource.Stop();

            _fishingReelPressSource.clip = entry.clip;
            _fishingReelPressSource.volume = Mathf.Clamp01(entry.volumeScale * _userSfxLinear01);
            _fishingReelPressSource.pitch = 1f;
            _fishingReelPressSource.spatialBlend = 0f;
            _fishingReelPressSource.loop = false;
            _fishingReelPressSource.Play();
            _lastFishingReelPressUnscaledTime = Time.unscaledTime;
        }

        void EnsureFishingReelPressSource()
        {
            if (_fishingReelPressSource != null)
                return;

            var go = new GameObject("FishingReelPressSfx");
            go.transform.SetParent(transform, false);
            _fishingReelPressSource = go.AddComponent<AudioSource>();
            _fishingReelPressSource.playOnAwake = false;
            _fishingReelPressSource.spatialBlend = 0f;
            if (sfxOutputGroup != null)
                _fishingReelPressSource.outputAudioMixerGroup = sfxOutputGroup;
        }

        void EnsureLoopingSfxSource()
        {
            if (_loopingSfxSource != null)
                return;

            var go = new GameObject("LoopingSfx");
            go.transform.SetParent(transform, false);
            _loopingSfxSource = go.AddComponent<AudioSource>();
            _loopingSfxSource.playOnAwake = false;
            _loopingSfxSource.spatialBlend = 0f;
            _loopingSfxSource.loop = true;
            if (sfxOutputGroup != null)
                _loopingSfxSource.outputAudioMixerGroup = sfxOutputGroup;
        }

        public void PlayLoopingSfx(SfxId id, float volumeMultiplier = 1f)
        {
            if (id == SfxId.None || !_map.TryGetValue(id, out var entry) || entry.clip == null)
                return;

            EnsureLoopingSfxSource();
            if (_loopingSfxSource.isPlaying && _loopingSfxId == id)
                return;

            _loopingSfxSource.Stop();
            _loopingSfxId = id;
            _loopingSfxSource.clip = entry.clip;
            _loopingSfxSource.volume = Mathf.Clamp01(entry.volumeScale * volumeMultiplier * _userSfxLinear01);
            _loopingSfxSource.pitch = 1f;
            _loopingSfxSource.loop = true;
            _loopingSfxSource.Play();
        }

        public void StopLoopingSfx()
        {
            _loopingSfxId = SfxId.None;
            if (_loopingSfxSource != null && _loopingSfxSource.isPlaying)
                _loopingSfxSource.Stop();
        }

        private AudioSource NextFreeSfxSource()
        {
            if (_sfxPool == null || _sfxPool.Length == 0)
                return null;

            for (var n = 0; n < _sfxPool.Length; n++)
            {
                var idx = (_sfxCursor + n) % _sfxPool.Length;
                if (!_sfxPool[idx].isPlaying)
                {
                    _sfxCursor = (idx + 1) % _sfxPool.Length;
                    return _sfxPool[idx];
                }
            }

            _sfxCursor = (_sfxCursor + 1) % _sfxPool.Length;
            return _sfxPool[_sfxCursor];
        }

        public void SetMasterVolumeDb(float db)
        {
            if (mainMixer == null || string.IsNullOrEmpty(exposedParamMasterDb))
                return;
            mainMixer.SetFloat(exposedParamMasterDb, db);
        }

        public void SetBgmVolumeDb(float db)
        {
            if (mainMixer == null || string.IsNullOrEmpty(exposedParamBgmDb))
                return;
            mainMixer.SetFloat(exposedParamBgmDb, db);
        }

        public void SetSfxVolumeDb(float db)
        {
            if (mainMixer == null || string.IsNullOrEmpty(exposedParamSfxDb))
                return;
            mainMixer.SetFloat(exposedParamSfxDb, db);
        }

        private void FlushAmbientWind()
        {
            var target = _windDriveReported ? _windDriveReportedPeak : 0f;
            _windDriveReportedPeak = 0f;
            _windDriveReported = false;

            var speed = target > _windSmoothedDrive01 ? windRiseSpeed : windFallSpeed;
            _windSmoothedDrive01 = Mathf.MoveTowards(_windSmoothedDrive01, target, Time.deltaTime * speed);

            if (!_map.TryGetValue(SfxId.AmbientWind, out var entry) || entry.clip == null)
            {
                StopWindLoopHard();
                return;
            }

            if (_windSmoothedDrive01 < 0.002f)
            {
                StopWindLoopHard();
                return;
            }

            EnsureWindLoopSource(entry.clip);
            var t = Mathf.Clamp01(_windSmoothedDrive01);
            var linearVol = Mathf.Lerp(0.01f, 1f, t) * Mathf.Clamp01(entry.volumeScale) * _userSfxLinear01;
            _windLoopSource.volume = linearVol;
            _windLoopSource.pitch = Mathf.Lerp(windPitchMin, windPitchMax, t);
            if (!_windLoopSource.isPlaying)
                _windLoopSource.Play();
        }

        private void EnsureWindLoopSource(AudioClip clip)
        {
            if (_windLoopSource != null)
            {
                if (_windLoopSource.clip != clip)
                {
                    _windLoopSource.Stop();
                    _windLoopSource.clip = clip;
                }

                return;
            }

            var go = new GameObject("AmbientWind");
            go.transform.SetParent(transform, false);
            _windLoopSource = go.AddComponent<AudioSource>();
            _windLoopSource.playOnAwake = false;
            _windLoopSource.loop = true;
            _windLoopSource.spatialBlend = 0f;
            _windLoopSource.clip = clip;
            _windLoopSource.outputAudioMixerGroup = sfxOutputGroup;
        }

        private void StopWindLoopHard()
        {
            _windSmoothedDrive01 = 0f;
            if (_windLoopSource == null)
                return;
            _windLoopSource.Stop();
        }
    }
}
