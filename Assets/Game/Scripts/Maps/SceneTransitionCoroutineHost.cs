using System;
using System.Collections;
using Game.Core;
using Game.Core.Content;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Maps
{
    /// <summary>
    /// DontDestroyOnLoad：在当前场景上播放遮罩 → 遮罩覆盖足够后卸载旧场景 → 加载完成后再激活目标场景 → 遮罩缩小揭开。
    /// </summary>
    public sealed class SceneTransitionCoroutineHost : MonoBehaviour
    {
        static SceneTransitionCoroutineHost _instance;

        AsyncOperation _pendingSceneOp;
        bool _useAddressablesActivation;
        bool _sourceScenesUnloaded;

        public static event Action<float> LoadProgressChanged;

        /// <summary>加载阶段文案（场景加载）。</summary>
        public static event Action<string> LoadStatusChanged;

        /// <summary>场景跳转遮罩开始覆盖前触发；供跑步机等玩法打断。</summary>
        public static event Action SceneTransitionStarting;

        public static void EnsureExists()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(SceneTransitionCoroutineHost));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneTransitionCoroutineHost>();
        }

        public static void BeginNavigation(string gameplaySceneName, SceneNavigationSettings cfg)
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
                return;

            EnsureExists();
            _instance.StopAllCoroutines();
            SceneTransitionStarting?.Invoke();
            if (string.IsNullOrWhiteSpace(SceneTransitionRequest.PendingGameplayScene))
                SceneTransitionRequest.PrepareNavigation(gameplaySceneName.Trim());
            _instance.StartCoroutine(_instance.CoBeginNavigation(gameplaySceneName.Trim(), cfg));
        }

        public static void StartLoadPendingGameplay(SceneNavigationSettings settingsOverride, bool assumeAlreadyFullScreenCovered = false)
        {
            EnsureExists();
            _instance.StopAllCoroutines();
            _instance.StartCoroutine(_instance.CoLoadGameplay(settingsOverride, assumeAlreadyFullScreenCovered));
        }

        IEnumerator CoBeginNavigation(string targetScene, SceneNavigationSettings cfg)
        {
            cfg ??= SceneNavigationSettings.ResolveActive();

            if (string.IsNullOrWhiteSpace(SceneTransitionRequest.PendingGameplayScene))
                SceneTransitionRequest.PrepareNavigation(targetScene);

            yield return EnsureTransitionOverlayReady(cfg);
            if (!TransitionOverlayBinding.HasInstance)
                yield break;

            yield return CoLoadGameplay(cfg, assumeAlreadyFullScreenCovered: false);
        }

        IEnumerator CoLoadGameplay(SceneNavigationSettings settingsOverride, bool assumeAlreadyFullScreenCovered)
        {
            _pendingSceneOp = null;
            _useAddressablesActivation = false;
            _sourceScenesUnloaded = false;

            var cfg = settingsOverride != null ? settingsOverride : SceneNavigationSettings.ResolveActive();

            var target = SceneTransitionRequest.PendingGameplayScene;
            SceneTransitionRequest.ClearPending();

            if (string.IsNullOrWhiteSpace(target))
            {
                Debug.LogError(
                    $"{nameof(SceneTransitionCoroutineHost)}: 没有待加载的场景名（Pending 为空）。请勿重复触发过场加载。");
                LoadProgressChanged?.Invoke(0f);
                yield break;
            }

            target = target.Trim();

            Debug.Log($"[{nameof(SceneTransitionCoroutineHost)}] 过场加载：目标场景={target}");

            if (!GameSceneLoader.CanLoadScene(target))
            {
                var fallback = GameSceneLoader.ResolveGameplaySceneFromSave();
                if (!string.IsNullOrWhiteSpace(fallback) && GameSceneLoader.CanLoadScene(fallback))
                {
                    Debug.LogWarning(
                        $"{nameof(SceneTransitionCoroutineHost)}: 场景「{target}」不可加载，改用「{fallback}」。");
                    target = fallback.Trim();
                }
                else
                {
                    Debug.LogError(
                        $"{nameof(SceneTransitionCoroutineHost)}: 场景「{target}」不可加载（Build Settings 未配置），且无法解析可用 fallback。",
                        this);
                    yield break;
                }
            }

            yield return EnsureTransitionOverlayReady(cfg);
            if (!TransitionOverlayBinding.HasInstance)
                yield break;

            LoadProgressChanged?.Invoke(0f);
            LoadStatusChanged?.Invoke("正在加载…");

            var coverFinished = false;
            var loadFinished = false;
            var unloadThreshold = cfg != null ? cfg.unloadSourceSceneCoverThreshold : 0.85f;
            unloadThreshold = Mathf.Clamp(unloadThreshold, 0.5f, 1f);

            if (assumeAlreadyFullScreenCovered && SceneTransitionOverlay.IsScreenCoveredForActivation)
            {
                coverFinished = true;
                TryUnloadSourceScenes(unloadThreshold, force: true);
            }
            else
            {
                StartCoroutine(CoRunCoverWithUnload(cfg, unloadThreshold, () => coverFinished = true));
            }

            StartCoroutine(CoRunLoadPipeline(cfg, target, () => loadFinished = true));

            while (!coverFinished || !loadFinished)
                yield return null;

            if (!_sourceScenesUnloaded)
                TryUnloadSourceScenes(1f, force: true);

            LoadStatusChanged?.Invoke("正在进入场景…");
            yield return CoActivatePreparedScene(target);

            LoadProgressChanged?.Invoke(1f);

            FairyGuiSceneLifecycle.PruneAfterGameplaySceneActivated();

            var postHold = cfg != null ? cfg.postLoadHoldBeforeRevealSeconds : 0f;
            postHold = Mathf.Max(0f, postHold);
            if (postHold > 0f)
                yield return new WaitForSecondsRealtime(postHold);

            yield return SceneTransitionOverlay.RevealScreenRoutine(cfg);

            LoadProgressChanged?.Invoke(0f);
            LoadStatusChanged?.Invoke(string.Empty);
        }

        IEnumerator CoRunCoverWithUnload(SceneNavigationSettings cfg, float unloadThreshold, Action onFinished)
        {
            yield return SceneTransitionOverlay.CoverScreenRoutine(cfg, cover01 =>
            {
                if (!_sourceScenesUnloaded && cover01 >= unloadThreshold)
                    TryUnloadSourceScenes(unloadThreshold, force: false);
            });

            onFinished?.Invoke();
        }

        void TryUnloadSourceScenes(float threshold, bool force)
        {
            if (_sourceScenesUnloaded)
                return;

            _sourceScenesUnloaded = true;
            Debug.Log(
                $"[{nameof(SceneTransitionCoroutineHost)}] 遮罩进度已达 {threshold:P0}，卸载当前已加载场景（过场 UI 保留）。");
            UnloadAllLoadedScenes();
        }

        static void UnloadAllLoadedScenes()
        {
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                SceneManager.UnloadSceneAsync(scene);
            }
        }

        IEnumerator CoRunLoadPipeline(
            SceneNavigationSettings cfg,
            string target,
            Action onFinished)
        {
            var useAddressables = GameSceneLoader.ShouldUseAddressablesForScene(target);

            LoadStatusChanged?.Invoke("正在加载场景…");

            const float sceneWeight = 1f;
            const float sceneBase = 0f;

            if (useAddressables)
            {
                _useAddressablesActivation = true;
                yield return GameAddressableSceneLoader.CoPrepareSceneSingle(
                    target,
                    scene01 => LoadProgressChanged?.Invoke(Mathf.Clamp01(sceneBase + scene01 * sceneWeight)));

                if (!GameAddressableSceneLoader.HasPreparedScene)
                {
                    GameContentLoadUtility.ShowResourceLoadFailedToast();
                    onFinished?.Invoke();
                    yield break;
                }
            }
            else
            {
                _useAddressablesActivation = false;
                var op = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
                if (op == null)
                {
                    Debug.LogError(
                        $"{nameof(SceneTransitionCoroutineHost)}: LoadSceneAsync 返回 null，场景「{target}」是否在 Build Settings 中？",
                        this);
                    GameContentLoadUtility.ShowResourceLoadFailedToast();
                    onFinished?.Invoke();
                    yield break;
                }

                op.allowSceneActivation = false;
                _pendingSceneOp = op;

                while (op.progress < 0.9f)
                {
                    var scene01 = Mathf.Clamp01(op.progress / 0.9f);
                    LoadProgressChanged?.Invoke(Mathf.Clamp01(sceneBase + scene01 * sceneWeight));
                    yield return null;
                }

                LoadProgressChanged?.Invoke(Mathf.Clamp01(sceneBase + sceneWeight));
            }

            onFinished?.Invoke();
        }

        IEnumerator CoActivatePreparedScene(string target)
        {
            if (_useAddressablesActivation)
            {
                if (!GameAddressableSceneLoader.HasPreparedScene)
                {
                    Debug.LogError($"{nameof(SceneTransitionCoroutineHost)}: Addressables 场景「{target}」未就绪。", this);
                    GameContentLoadUtility.ShowResourceLoadFailedToast();
                    yield break;
                }

                yield return GameAddressableSceneLoader.CoActivatePreparedScene(
                    p => LoadProgressChanged?.Invoke(Mathf.Clamp01(p)));
                yield break;
            }

            if (_pendingSceneOp == null)
            {
                Debug.LogError($"{nameof(SceneTransitionCoroutineHost)}: 场景「{target}」加载操作丢失。", this);
                yield break;
            }

            _pendingSceneOp.allowSceneActivation = true;
            yield return _pendingSceneOp;
            _pendingSceneOp = null;
        }

        static IEnumerator EnsureTransitionOverlayReady(SceneNavigationSettings cfg)
        {
            if (TransitionOverlayBinding.HasInstance)
                yield break;

            if (cfg == null)
            {
                Debug.LogError(
                    $"{nameof(SceneTransitionCoroutineHost)}: {nameof(SceneNavigationSettings)} 为 null，无法解析过场场景。");
                yield break;
            }

            yield return null;

            if (!TransitionOverlayBinding.HasInstance)
            {
                var found = UnityEngine.Object.FindObjectOfType<TransitionOverlayBinding>(true);
                if (found != null)
                    found.TryClaimSingleton();
            }

            if (TransitionOverlayBinding.HasInstance)
                yield break;

            if (!cfg.useTransitionScene || string.IsNullOrWhiteSpace(cfg.transitionSceneName))
            {
                Debug.LogError(
                    $"{nameof(SceneTransitionCoroutineHost)}: 未找到 {nameof(TransitionOverlayBinding)}，且未配置过场场景。");
                yield break;
            }

            var sceneName = cfg.transitionSceneName.Trim();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name == sceneName)
                {
                    SuppressTransitionSceneRendering(s);
                    var found = UnityEngine.Object.FindObjectOfType<TransitionOverlayBinding>(true);
                    if (found != null)
                        found.TryClaimSingleton();
                    yield break;
                }
            }

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError(
                    $"{nameof(SceneTransitionCoroutineHost)}: 无法加法加载过场场景「{sceneName}」。");
                yield break;
            }

            while (!op.isDone)
                yield return null;

            var loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid())
                SuppressTransitionSceneRendering(loadedScene);

            if (!TransitionOverlayBinding.HasInstance)
            {
                var found = UnityEngine.Object.FindObjectOfType<TransitionOverlayBinding>(true);
                if (found != null)
                    found.TryClaimSingleton();
            }

            if (!TransitionOverlayBinding.HasInstance)
            {
                Debug.LogError(
                    $"{nameof(SceneTransitionCoroutineHost)}: 过场场景「{sceneName}」中未找到 {nameof(TransitionOverlayBinding)}。");
            }
        }

        static void SuppressTransitionSceneRendering(Scene transitionScene)
        {
            if (!transitionScene.IsValid() || !transitionScene.isLoaded)
                return;

            var roots = transitionScene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                if (root.GetComponentInChildren<TransitionOverlayBinding>(true) != null)
                    continue;

                var cameras = root.GetComponentsInChildren<Camera>(true);
                for (var c = 0; c < cameras.Length; c++)
                {
                    if (cameras[c] != null)
                        cameras[c].enabled = false;
                }
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
