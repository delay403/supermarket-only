using System;
using System.Collections;
using Game.Collectibles.Equipment;
using Game.Player;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 钓获后：鱼挂到 HandFishRoot，播放展示动画，时长结束后结束展示。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFishingCatchShowcaseDriver : MonoBehaviour
    {
        [SerializeField] private PlayerFishingLineDriver lineDriver;
        [SerializeField] private PlayerFishingAnimatorDriver animatorDriver;
        [SerializeField] private EquipmentSpawner equipment;
        [SerializeField] private PlayerRig rig;

        [Header("展示时长")]
        [Tooltip("HandFishRoot 展示动画持续时间（秒）；Toast/镜头/庆祝特效与展示同时开始。")]
        [SerializeField] private float catchShowcaseDurationSeconds = 2.5f;
        [Tooltip("展示结束前提前隐藏 HandFishRoot 鱼模型的时间（秒）。")]
        [SerializeField] private float handFishHideBeforeShowcaseEndSeconds = 0.5f;

        [Header("HandFishRoot 鱼模型局部姿态")]
        [SerializeField] private Vector3 handFishLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 handFishLocalEuler = new Vector3(-90f, 180f, 0f);
        [Tooltip("进入展示后若尚未挂到 HandFishRoot，持续重试挂接的时间（秒）。")]
        [SerializeField] private float handFishAttachRetrySeconds = 0.75f;

        Transform _attachedHandSocket;

        FishingCatchLineFollower _activeFollower;
        Coroutine _showcaseRoutine;

        public float CatchShowcaseDurationSeconds => Mathf.Max(0.05f, catchShowcaseDurationSeconds);

        public bool IsCatchShowcaseActive => _showcaseRoutine != null;

        public event Action CatchShowcaseStarted;
        public event Action CatchShowcaseEnded;

        void Awake()
        {
            if (lineDriver == null) lineDriver = GetComponent<PlayerFishingLineDriver>();
            if (animatorDriver == null) animatorDriver = GetComponent<PlayerFishingAnimatorDriver>();
            if (equipment == null) equipment = GetComponent<EquipmentSpawner>();
            if (rig == null) rig = GetComponentInChildren<PlayerRig>(true);
        }

        void OnEnable()
        {
            if (lineDriver != null)
                lineDriver.CatchFollowerReadyForHandShowcase += OnFollowerReadyForHandShowcase;
        }

        void OnDisable()
        {
            if (lineDriver != null)
                lineDriver.CatchFollowerReadyForHandShowcase -= OnFollowerReadyForHandShowcase;
            ForceStopShowcase(notifyEnded: false);
        }

        void OnFollowerReadyForHandShowcase(FishingCatchLineFollower follower)
        {
            if (follower == null)
                return;

            if (_showcaseRoutine != null)
                ForceStopShowcase(notifyEnded: false);

            _activeFollower = follower;
            TryAttachHandFish(follower);

            var lineVisual = lineDriver != null ? lineDriver.LineVisual : null;
            lineVisual?.EndCatchRetrieval();
            lineVisual?.SetRetractedImmediate();

            animatorDriver?.PlayCatchShowcase();
            CatchShowcaseStarted?.Invoke();
            _showcaseRoutine = StartCoroutine(CoRunShowcase());
        }

        IEnumerator CoRunShowcase()
        {
            var attachDeadline = Time.time + Mathf.Max(0f, handFishAttachRetrySeconds);
            var total = CatchShowcaseDurationSeconds;
            var fishVisibleSeconds = ResolveHandFishVisibleSeconds(total);
            var fishHidden = false;
            var attachWarningLogged = false;
            var elapsed = 0f;

            while (elapsed < total)
            {
                if (_activeFollower != null
                    && !IsFollowerAttachedToHand(_activeFollower)
                    && Time.time <= attachDeadline)
                {
                    TryAttachHandFish(_activeFollower);
                }
                else if (_activeFollower != null
                         && !IsFollowerAttachedToHand(_activeFollower)
                         && !attachWarningLogged
                         && Time.time > attachDeadline)
                {
                    attachWarningLogged = true;
                    Debug.LogWarning(
                        "[PlayerFishingCatchShowcaseDriver] 未能挂到 HandFishRoot，请确认角色预制体含 HandFishRoot 节点。",
                        this);
                }

                if (_activeFollower != null
                    && !fishHidden
                    && elapsed >= fishVisibleSeconds)
                {
                    fishHidden = true;
                    HideActiveHandFish();
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!fishHidden)
                HideActiveHandFish();

            _showcaseRoutine = null;
            CatchShowcaseEnded?.Invoke();
            CompleteActiveHandFishFollower();
        }

        void TryAttachHandFish(FishingCatchLineFollower follower)
        {
            if (follower == null)
                return;

            var socket = ResolveHandFishSocket();
            if (socket == null)
                return;

            follower.AttachToHandFishRoot(socket, handFishLocalPosition, handFishLocalEuler);
            _attachedHandSocket = socket;
        }

        bool IsFollowerAttachedToHand(FishingCatchLineFollower follower)
        {
            if (follower == null || _attachedHandSocket == null)
                return false;

            return follower.transform.parent == _attachedHandSocket;
        }

        Transform ResolveLiveCharacterRoot()
        {
            if (rig == null)
                rig = GetComponentInChildren<PlayerRig>(true);

            rig?.EnsureSceneSocketsResolved();
            var socket = rig != null ? rig.characterSocket : null;
            if (socket == null)
                return null;

            for (var i = 0; i < socket.childCount; i++)
            {
                var child = socket.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                    return child;
            }

            return socket.childCount > 0 ? socket.GetChild(0) : null;
        }

        float ResolveHandFishVisibleSeconds(float totalDuration)
        {
            var hideEarly = Mathf.Max(0f, handFishHideBeforeShowcaseEndSeconds);
            return Mathf.Clamp(totalDuration - hideEarly, 0.05f, totalDuration);
        }

        void HideActiveHandFish()
        {
            if (_activeFollower == null)
                return;

            _activeFollower.HideHandFishVisual();
        }

        void CompleteActiveHandFishFollower()
        {
            if (_activeFollower == null)
                return;

            _activeFollower.NotifyHandShowcaseComplete();
            _activeFollower = null;
            _attachedHandSocket = null;
        }

        public void ForceStopShowcase(bool notifyEnded = false)
        {
            if (_showcaseRoutine != null)
            {
                StopCoroutine(_showcaseRoutine);
                _showcaseRoutine = null;
            }

            if (notifyEnded)
                CatchShowcaseEnded?.Invoke();

            CompleteActiveHandFishFollower();
        }

        Transform ResolveHandFishSocket()
        {
            if (rig == null)
                rig = GetComponentInChildren<PlayerRig>(true);
            rig?.EnsureSceneSocketsResolved();

            var characterRoot = ResolveLiveCharacterRoot();
            if (characterRoot != null)
            {
                if (rig != null)
                    CharacterHierarchySockets.BindHandFishSocketUnderCharacter(rig, characterRoot);

                var handFish = CharacterHierarchySockets.FindDeepChildByName(
                    characterRoot,
                    CharacterHierarchySockets.HandFishRootTransformName);
                if (handFish != null)
                    return handFish;
            }

            if (rig != null && rig.handFishSocket != null)
                return rig.handFishSocket;

            if (rig != null && rig.rodHandSocket != null)
                return rig.rodHandSocket;

            return CharacterHierarchySockets.FindDeepChildByName(
                transform,
                CharacterHierarchySockets.HandFishRootTransformName);
        }
    }
}
