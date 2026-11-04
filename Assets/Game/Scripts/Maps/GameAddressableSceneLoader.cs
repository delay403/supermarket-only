using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Maps
{
    /// <summary>
    /// 通过 Addressables 加载未加入 Build Settings 的玩法场景。
    /// </summary>
    public static class GameAddressableSceneLoader
    {
        static AsyncOperationHandle<SceneInstance> s_activeHandle;

        public static bool HasPreparedScene => s_activeHandle.IsValid()
                                               && s_activeHandle.Status == AsyncOperationStatus.Succeeded;

        public static IEnumerator CoPrepareSceneSingle(string sceneName, Action<float> reportProgress)
        {
            ReleaseActiveHandleIfNeeded();

            var primary = GameSceneLoader.ResolveAddressableSceneAddress(sceneName);
            var candidates = string.IsNullOrWhiteSpace(primary)
                ? new[] { sceneName }
                : primary.Equals(sceneName, StringComparison.OrdinalIgnoreCase)
                    ? new[] { primary }
                    : new[] { primary, sceneName };

            for (var i = 0; i < candidates.Length; i++)
            {
                var address = candidates[i];
                if (string.IsNullOrWhiteSpace(address))
                    continue;

                var succeeded = false;
                yield return CoTryPrepareByAddress(address, reportProgress, result => succeeded = result);
                if (succeeded)
                    yield break;
            }

            Debug.LogError(
                $"[{nameof(GameAddressableSceneLoader)}] 所有 Addressable 地址均加载失败: {sceneName}");
        }

        public static IEnumerator CoActivatePreparedScene(Action<float> reportProgress = null)
        {
            if (!HasPreparedScene)
                yield break;

            reportProgress?.Invoke(0.95f);

            var activation = s_activeHandle.Result.ActivateAsync();
            while (!activation.isDone)
            {
                reportProgress?.Invoke(Mathf.Clamp01(0.95f + activation.progress * 0.05f));
                yield return null;
            }

            reportProgress?.Invoke(1f);
        }

        public static IEnumerator CoLoadSceneSingle(string sceneName, Action<float> reportProgress)
        {
            yield return CoPrepareSceneSingle(sceneName, reportProgress);
            if (!HasPreparedScene)
                yield break;

            yield return CoActivatePreparedScene(reportProgress);
        }

        static IEnumerator CoTryPrepareByAddress(string address, Action<float> reportProgress, Action<bool> onComplete)
        {
            var handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Single, activateOnLoad: false);
            s_activeHandle = handle;

            while (!handle.IsDone)
            {
                reportProgress?.Invoke(Mathf.Clamp01(handle.PercentComplete));
                yield return null;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning(
                    $"[{nameof(GameAddressableSceneLoader)}] Addressable 场景加载失败: {address} status={handle.Status}");
                ReleaseActiveHandleIfNeeded();
                onComplete?.Invoke(false);
                yield break;
            }

            reportProgress?.Invoke(0.95f);
            onComplete?.Invoke(true);
        }

        static void ReleaseActiveHandleIfNeeded()
        {
            if (!s_activeHandle.IsValid())
                return;

            Addressables.Release(s_activeHandle);
            s_activeHandle = default;
        }
    }
}
