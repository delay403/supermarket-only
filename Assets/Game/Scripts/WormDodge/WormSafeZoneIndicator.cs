using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// 进入安全区时在角色身上显示标记物体，离开时隐藏。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormSafeZoneIndicator : MonoBehaviour
    {
        const float PollIntervalSeconds = 0.2f;

        [SerializeField] private GameObject indicatorRoot;

        WormDodgeParticipant _participant;
        bool _lastVisible;
        float _nextPollTime;

        void Awake()
        {
            _nextPollTime = Time.time + (GetInstanceID() & 0xFF) * 0.004f;
            Initialize();
        }

        void Update()
        {
            if (Time.time < _nextPollTime)
                return;

            _nextPollTime = Time.time + PollIntervalSeconds;

            if (_participant == null || indicatorRoot == null)
            {
                if (_participant == null)
                    ResolveParticipant();
                if (indicatorRoot == null)
                    ResolveIndicatorIfNeeded();
                return;
            }

            ApplyVisibilityIfChanged();
        }

        void OnDisable()
        {
            if (indicatorRoot != null)
                indicatorRoot.SetActive(false);

            _lastVisible = false;
        }

        public void Configure(GameObject root)
        {
            indicatorRoot = root;
            ApplyVisibilityImmediate();
        }

        void Initialize()
        {
            ResolveParticipant();
            ResolveIndicatorIfNeeded();
            ApplyVisibilityImmediate();
        }

        void ApplyVisibilityImmediate()
        {
            if (indicatorRoot == null || _participant == null)
                return;

            ApplyVisibilityIfChanged(force: true);
        }

        void ApplyVisibilityIfChanged(bool force = false)
        {
            if (indicatorRoot == null || _participant == null)
                return;

            var visible = _participant.IsInSafeZone;
            if (!force && visible == _lastVisible)
                return;

            _lastVisible = visible;
            indicatorRoot.SetActive(visible);
        }

        void ResolveParticipant()
        {
            if (_participant != null)
                return;

            _participant = GetComponent<WormDodgeParticipant>();
            if (_participant != null)
                return;

            _participant = GetComponentInParent<WormDodgeParticipant>();
        }

        void ResolveIndicatorIfNeeded()
        {
            if (indicatorRoot != null)
                return;

            var marker = transform.Find("SafeZoneIndicator");
            if (marker == null)
            {
                var children = GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < children.Length; i++)
                {
                    if (children[i].name == "SafeZoneIndicator")
                    {
                        marker = children[i];
                        break;
                    }
                }
            }

            if (marker != null)
                indicatorRoot = marker.gameObject;
        }
    }
}
