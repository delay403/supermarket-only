using System.Collections.Generic;
using Game.Audio;
using Game.Core.Config;
using Game.Core.Content;
using Game.Save;
using Game.VFX;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    [DefaultExecutionOrder(-10000)]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        static bool s_booted;

        [Header("Configuration")]
        [SerializeField] GameBalanceConfig balanceOverride;
        [SerializeField] SupermarketOrganize.Config.SupermarketOrganizeConfig supermarketOrganizeOverride;
        [SerializeField] SupermarketOrganize.Config.SupermarketItemDatabase supermarketItemsOverride;
        [SerializeField] SupermarketOrganize.Config.SupermarketMapDatabase supermarketMapsOverride;

        [Header("Runtime services")]
        [SerializeField] List<GameplayVfxManager.VfxPrefabBinding> bootstrapVfxBindings;
        [SerializeField] List<AudioManager.SfxClipEntry> bootstrapSfxLibrary;
        [SerializeField] AudioClip bootstrapBgmClip;
        [SerializeField] bool playBootstrapBgmIfClipSet = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateFallbackIfSceneHasNone()
        {
            if (s_booted)
                return;
            var go = new GameObject(nameof(GameBootstrapper));
            DontDestroyOnLoad(go);
            go.AddComponent<GameBootstrapper>();
        }

        void Awake()
        {
            if (s_booted)
            {
                Destroy(gameObject);
                return;
            }

            s_booted = true;
            DontDestroyOnLoad(gameObject);
            GameContentService.InitializeFromSettings();

            var binder = FindObjectOfType<GameModeConfigBinder>(true);
            if (binder != null)
                GameContentService.RegisterBinderAssets(binder);

            var input = new ConfigRegistry.Input
            {
                balance = balanceOverride,
                supermarketOrganize = supermarketOrganizeOverride,
                supermarketItems = supermarketItemsOverride,
                supermarketMaps = supermarketMapsOverride,
            };
            if (binder != null)
                input = binder.BuildConfigRegistryInput(input);
            ConfigRegistry.Initialize(input);

            SaveService.Initialize();
            SaveService.LoadOrCreate();

            GameplayVfxManager.EnsureCreated();
            if (bootstrapVfxBindings != null && bootstrapVfxBindings.Count > 0)
                GameplayVfxManager.ReplaceRuntimeBindings(bootstrapVfxBindings);

            AudioManager.EnsureCreated();
            var audio = AudioManager.Instance;
            if (bootstrapSfxLibrary != null && bootstrapSfxLibrary.Count > 0)
                audio.ReplaceSfxLibrary(bootstrapSfxLibrary);
            if (bootstrapBgmClip != null && playBootstrapBgmIfClipSet)
                audio.PlayBgm(bootstrapBgmClip, true, 0f);
            audio.ApplyUserVolumeFromSave();

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshScene(SceneManager.GetActiveScene());
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveService.Save();
        }

        void OnApplicationQuit()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SaveService.Save();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshScene(scene);

        static void RefreshScene(Scene scene)
        {
            if (!scene.IsValid())
                return;

            foreach (var binder in Object.FindObjectsOfType<GameModeConfigBinder>(true))
                if (binder != null && binder.gameObject.scene == scene)
                    ConfigRegistry.ApplySceneBinder(binder);

            foreach (var binder in Object.FindObjectsOfType<GameModeRuntimeBinder>(true))
                if (binder != null && binder.gameObject.scene == scene)
                    binder.ApplyToRuntimeServices();

            SupermarketOrganize.SupermarketOrganizePlayerSetup.RefreshSceneServices();
        }
    }
}
