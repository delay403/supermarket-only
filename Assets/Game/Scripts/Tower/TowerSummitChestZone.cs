using System.Collections;
using DG.Tweening;
using Game.Audio;
using Game.Core;
using Game.Glide;
using Game.Player;
using Game.UI;
using UnityEngine;

namespace Game.Tower
{
    /// <summary>
    /// 挂在塔顶逻辑物体上（可与碰撞体不同物体）。将 <see cref="summitTriggerCollider"/> 设为「登顶检测」用触发器（通常为子物体上的 BoxCollider）。
    /// 若未指定则使用本物体上的任意 <see cref="Collider"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerSummitChestZone : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TowerManager tower;
        [SerializeField] private Transform chestLid;
        [Tooltip("登顶判定的触发器。可与脚本不在同一物体（推荐单独空物体 + Trigger）。不填则尝试用本物体上的 Collider。")]
        [SerializeField] private Collider summitTriggerCollider;
        [Tooltip("部分环境下 Trigger 与 CharacterController 需运动体才稳定收到事件；给触发器所在物体自动加 Kinematic Rigidbody（仅未挂载 Rigidbody 时）。")]
        [SerializeField] private bool autoAddKinematicRigidbodyOnTriggerObject = true;

        [Header("Lid (local Euler X)")]
        [SerializeField] private float lidClosedEulerX = -90f;
        [SerializeField] private float lidOpenedEulerX = -150f;
        [SerializeField] private float lidTweenDurationSeconds = 0.45f;

        [Header("HUD")]
        [SerializeField] private float hudDelaySeconds = 2f;
        [SerializeField] private string hudTitle = "恭喜登顶";
        [SerializeField] private string hudContent = "恭喜你登顶了！！！你将获得大量奖励和奖杯！";
        [SerializeField] private string hudConfirmText = "跳跃";

        [Header("Audio")]
        [SerializeField] private SfxId chestOpenSfx = SfxId.SettlementWin;
        [SerializeField] [Range(0f, 2f)] private float chestOpenSfxVolume = 1f;

        bool _sequenceStarted;
        TowerSummitTriggerRelay _boundRelay;

        void Awake()
        {
            if (tower == null)
                tower = UnityObjectFinder.FindFirst<TowerManager>();

            SetupSummitTriggerBinding();
        }

        void OnDestroy()
        {
            if (_boundRelay != null)
                _boundRelay.ClearBoundZone();
        }

        void SetupSummitTriggerBinding()
        {
            if (summitTriggerCollider == null)
                summitTriggerCollider = GetComponent<Collider>();

            if (summitTriggerCollider == null)
            {
                Debug.LogWarning(
                    $"{nameof(TowerSummitChestZone)} on {name}: 未指定 {nameof(summitTriggerCollider)} 且本物体无 Collider，登顶触发不可用。请在 Inspector 指定 Trigger Collider。",
                    this);
                return;
            }

            if (!summitTriggerCollider.isTrigger)
                Debug.LogWarning(
                    $"{nameof(TowerSummitChestZone)}: {summitTriggerCollider.name} 的 Collider 未勾选 Is Trigger，可能无法按预期检测登顶。",
                    summitTriggerCollider);

            if (summitTriggerCollider.gameObject != gameObject)
            {
                if (autoAddKinematicRigidbodyOnTriggerObject &&
                    summitTriggerCollider.GetComponentInParent<Rigidbody>() == null)
                {
                    var rb = summitTriggerCollider.gameObject.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                _boundRelay = summitTriggerCollider.GetComponent<TowerSummitTriggerRelay>();
                if (_boundRelay == null)
                    _boundRelay = summitTriggerCollider.gameObject.AddComponent<TowerSummitTriggerRelay>();
                _boundRelay.Bind(this);
            }
        }

        /// <summary>TowerManager 换图/刷新配置时调用，允许新地图再次播动画。</summary>
        public void ResetForNewMapConfiguration()
        {
            _sequenceStarted = false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (summitTriggerCollider != null && summitTriggerCollider.gameObject != gameObject)
                return;
            HandleSummitTriggerEnter(other);
        }

        /// <summary>也可由 <see cref="TowerSummitTriggerRelay"/> 转发。</summary>
        internal void HandleSummitTriggerEnter(Collider other)
        {
            if (_sequenceStarted)
                return;
            if (other == null)
                return;
            if (other.GetComponentInParent<PlayerMotor>() == null)
                return;

            if (tower == null)
                tower = UnityObjectFinder.FindFirst<TowerManager>();
            if (tower == null)
                return;

            if (tower.MaxHeight <= 0f)
                return;
            if (tower.SummitGlideCompletedForSession)
                return;

            var glide = other.GetComponentInParent<GlideController>();
            if (glide == null)
                glide = UnityObjectFinder.FindFirst<GlideController>();
            if (glide == null)
                return;

            _sequenceStarted = true;
            StartCoroutine(CoSummitSequence(glide));
        }

        IEnumerator CoSummitSequence(GlideController glide)
        {
            if (chestOpenSfx != SfxId.None)
                AudioManager.Instance?.PlaySfx(chestOpenSfx, chestOpenSfxVolume);

            if (chestLid != null)
            {
                var e = chestLid.localEulerAngles;
                chestLid.localEulerAngles = new Vector3(lidClosedEulerX, e.y, e.z);
                chestLid.DOLocalRotate(new Vector3(lidOpenedEulerX, e.y, e.z), Mathf.Max(0.05f, lidTweenDurationSeconds))
                    .SetEase(Ease.OutQuad);
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, hudDelaySeconds));

            var tMgr = tower != null ? tower : UnityObjectFinder.FindFirst<TowerManager>();
            HUDMgr.ShowHUD(new HUDMessage
            {
                Title = hudTitle,
                Content = hudContent,
                BtnShow = true,
                MiddleBtnShow = true,
                ConfirmText = hudConfirmText,
                CloseOnConfirm = true,
                ConfirmAction = () =>
                {
                    tMgr?.MarkSummitGlideCompletedForSession();
                    glide?.StartGlideFromSummitHudUsingBackflipPath();
                },
            });
        }
    }

    /// <summary>
    /// 加在「登顶 Trigger」所在物体上，将触发事件转发给 <see cref="TowerSummitChestZone"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerSummitTriggerRelay : MonoBehaviour
    {
        TowerSummitChestZone _zone;

        public void Bind(TowerSummitChestZone zone) => _zone = zone;

        public void ClearBoundZone() => _zone = null;

        void OnTriggerEnter(Collider other)
        {
            _zone?.HandleSummitTriggerEnter(other);
        }
    }
}
