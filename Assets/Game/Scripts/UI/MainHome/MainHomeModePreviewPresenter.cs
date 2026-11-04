using System.Collections.Generic;
using Game.UI.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.MainHome
{
    /// <summary>主页选关左侧预览：按模式切换子预制体，铺满父级并播放弹力缩放。</summary>
    public sealed class MainHomeModePreviewPresenter : MonoBehaviour
    {
        const string ScaleRootName = "PreviewScaleRoot";

        [SerializeField] private RectTransform previewRoot;
        [SerializeField] private float restScale = 1.35f;
        [SerializeField] private float peakScale = 1.5f;
        [SerializeField] private float upDuration = 0.16f;
        [SerializeField] private float downDuration = 0.12f;

        readonly Dictionary<string, GameObject> _instances = new Dictionary<string, GameObject>();
        string _activeModeId;
        Coroutine _bounceRoutine;
        bool _pendingIntro;

        void Awake()
        {
            ResolvePreviewRoot();
        }

        void OnEnable()
        {
            if (_pendingIntro)
                TryPlayPendingIntro();
        }

        void ResolvePreviewRoot()
        {
            if (previewRoot != null)
                return;

            var found = transform.Find("PrewImage") ?? transform.Find("PreviewImage");
            previewRoot = found as RectTransform;
            if (previewRoot == null)
                previewRoot = transform as RectTransform;
        }

        public void Show(MainHomeGameModeEntry entry, bool animate)
        {
            ResolvePreviewRoot();
            if (previewRoot == null)
                return;

            var modeId = entry?.modeId;
            if (string.IsNullOrWhiteSpace(modeId))
            {
                HideAll();
                _activeModeId = null;
                _pendingIntro = false;
                return;
            }

            var modeChanged = _activeModeId != modeId;
            if (modeChanged)
            {
                HideAll();
                _activeModeId = modeId;
            }

            var instance = ResolveInstance(entry);
            if (instance == null)
                return;

            instance.SetActive(true);
            var scaleTarget = PrepareInstanceLayout(instance);
            if (scaleTarget == null)
                return;

            if (animate)
                PlayIntro(scaleTarget);
            else
            {
                _pendingIntro = false;
                scaleTarget.localScale = new Vector3(restScale, restScale, 1f);
            }
        }

        /// <summary>选关页显示时补播初始化动画（PrepareWhileHidden 期间协程不会跑）。</summary>
        public void PlayIntroForCurrent()
        {
            if (string.IsNullOrWhiteSpace(_activeModeId))
                return;

            if (!_instances.TryGetValue(_activeModeId, out var instance) || instance == null || !instance.activeSelf)
                return;

            var scaleTarget = PrepareInstanceLayout(instance);
            if (scaleTarget == null)
                return;

            RequestIntro(scaleTarget);
        }

        void RequestIntro(RectTransform scaleTarget)
        {
            if (isActiveAndEnabled)
                PlayIntro(scaleTarget);
            else
            {
                _pendingIntro = true;
                scaleTarget.localScale = Vector3.zero;
            }
        }

        void TryPlayPendingIntro()
        {
            if (!_pendingIntro || string.IsNullOrWhiteSpace(_activeModeId))
                return;

            if (!_instances.TryGetValue(_activeModeId, out var instance) || instance == null || !instance.activeSelf)
                return;

            var scaleTarget = PrepareInstanceLayout(instance);
            if (scaleTarget == null)
                return;

            PlayIntro(scaleTarget);
        }

        void PlayIntro(RectTransform scaleTarget)
        {
            _pendingIntro = false;
            _bounceRoutine = UguiBounceScaleTween.PlayFromZero(
                this, scaleTarget, restScale, peakScale, upDuration, downDuration, _bounceRoutine);
        }

        GameObject ResolveInstance(MainHomeGameModeEntry entry)
        {
            if (entry == null || entry.previewPrefab == null || previewRoot == null)
                return null;

            if (_instances.TryGetValue(entry.modeId, out var existing) && existing != null)
                return existing;

            var go = Instantiate(entry.previewPrefab, previewRoot);
            go.name = entry.previewPrefab.name;
            PrepareInstanceLayout(go);
            _instances[entry.modeId] = go;
            go.SetActive(false);
            return go;
        }

        RectTransform PrepareInstanceLayout(GameObject instance)
        {
            if (instance == null)
                return null;

            var root = instance.transform as RectTransform;
            if (root == null)
                return null;

            StretchFull(root);
            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;

            var scaleTarget = EnsureScaleTarget(root);
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            if (scaleTarget != null && scaleTarget != root)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scaleTarget);

            return scaleTarget;
        }

        static RectTransform EnsureScaleTarget(RectTransform root)
        {
            var existing = root.Find(ScaleRootName) as RectTransform;
            if (existing != null)
            {
                StretchFull(existing);
                return existing;
            }

            var scaleRootGo = new GameObject(ScaleRootName, typeof(RectTransform));
            var scaleRoot = scaleRootGo.GetComponent<RectTransform>();
            scaleRoot.SetParent(root, false);
            StretchFull(scaleRoot);

            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child == scaleRoot)
                    continue;
                child.SetParent(scaleRoot, false);
            }

            StretchFull(scaleRoot);
            return scaleRoot;
        }

        public void HideAll()
        {
            foreach (var pair in _instances)
            {
                if (pair.Value != null)
                    pair.Value.SetActive(false);
            }
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
