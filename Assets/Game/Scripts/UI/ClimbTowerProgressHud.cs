using Game.Collectibles.Characters;
using Game.Core;
using Game.Core.Config;
using Game.Save;
using Game.Tower;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// UGUI：攀爬塔进度。读取底部 / 顶部两 Label 在 <see cref="avatarRoot"/> 父 Rect 下的等效 Y 间距，
    /// 除以 <see cref="TowerManager.MaxHeight"/>（与地图 SO 一致），得到每攀爬 1m 对应 <see cref="RectTransform.anchoredPosition"/>.y 的变化量。
    /// 请将 avatar 预制对齐在 0m 刻度高度；脚本只改 <c>anchoredPosition.y</c>，保留 <c>x</c>。
    /// </summary>
    public sealed class ClimbTowerProgressHud : MonoBehaviour
    {
        [Header("Visibility")]
        [SerializeField] private GameObject panelRoot;

        [Header("Height anchors (手动摆放，勿由脚本移动)")]
        [Tooltip("0m 刻度（可与 Text 同物体）。")]
        [SerializeField] private RectTransform heightAnchorBottom;

        [Tooltip("地图 MaxTowerHeight 对应刻度（如 1500M）。")]
        [SerializeField] private RectTransform heightAnchorTop;

        [Header("Avatar")]
        [FormerlySerializedAs("playerMarker")]
        [SerializeField] private RectTransform avatarRoot;

        [FormerlySerializedAs("portraitImage")]
        [SerializeField] private Image avatarPortraitImage;

        TowerManager _tower;
        ClimbController _climb;
        string _lastPortraitCharacterId;

        bool _driveAvatar;
        float _heightMeters;
        float _maxHeightMeters;

        void OnEnable()
        {
            _lastPortraitCharacterId = null;
            Canvas.willRenderCanvases += OnCanvasWillRenderCanvases;
        }

        void OnDisable() => Canvas.willRenderCanvases -= OnCanvasWillRenderCanvases;

        void Awake()
        {
            if (panelRoot == null)
                panelRoot = gameObject;
        }

        void Start()
        {
            ResolveRefs();
            SetPanelVisible(false);
        }

        void LateUpdate()
        {
            ResolveRefs();

            if (_climb == null || _climb.State == ClimbController.ClimbState.Inactive)
            {
                _driveAvatar = false;
                SetPanelVisible(false);
                return;
            }

            SetPanelVisible(true);

            var maxH = _tower != null ? _tower.MaxHeight : 0f;
            if (maxH <= 0.01f)
            {
                _driveAvatar = false;
                SetPanelVisible(false);
                return;
            }

            if (heightAnchorBottom == null || heightAnchorTop == null || avatarRoot == null)
            {
                _driveAvatar = false;
                return;
            }

            var h = _tower != null
                ? Mathf.Max(0f, _climb.transform.position.y - _tower.BaseY)
                : 0f;

            RefreshAvatarSpriteIfNeeded();

            _heightMeters = h;
            _maxHeightMeters = maxH;
            _driveAvatar = true;
        }

        void OnCanvasWillRenderCanvases()
        {
            if (!isActiveAndEnabled || !_driveAvatar)
                return;
            if (heightAnchorBottom == null || heightAnchorTop == null || avatarRoot == null)
                return;

            ApplyAvatarAnchoredY(_heightMeters, _maxHeightMeters);
        }

        /// <summary>
        /// span = 顶刻度 Y − 底刻度 Y（均在 avatar 父节点坐标系下），perMeter = span / MaxHeight；
        /// anchoredPosition.y = bottomY + 当前高度 × perMeter。
        /// </summary>
        void ApplyAvatarAnchoredY(float heightMeters, float maxHeightMeters)
        {
            var parent = avatarRoot.parent as RectTransform;
            if (parent == null)
                return;

            var bottomY = LabelReferenceLocalY(heightAnchorBottom, parent);
            var topY = LabelReferenceLocalY(heightAnchorTop, parent);
            var span = topY - bottomY;
            if (Mathf.Abs(span) < 1e-5f)
                return;

            var denom = Mathf.Max(1e-4f, maxHeightMeters);
            var perMeter = span / denom;

            var y = bottomY + Mathf.Clamp(heightMeters, 0f, denom) * perMeter;

            var ap = avatarRoot.anchoredPosition;
            ap.y = y;
            avatarRoot.anchoredPosition = ap;
        }

        /// <summary>
        /// Label 矩形中心在 <paramref name="avatarParent"/> 本地空间下的 Y，可与 avatar 的 <see cref="RectTransform.anchoredPosition"/>.y 对齐使用。
        /// 若 Label 与 avatar 同父，则等价于 Label 的 anchoredPosition.y。
        /// </summary>
        static float LabelReferenceLocalY(RectTransform label, RectTransform avatarParent)
        {
            if (label == null || avatarParent == null)
                return 0f;

            if (label.parent == avatarParent)
                return label.anchoredPosition.y;

            var world = label.TransformPoint(label.rect.center);
            return avatarParent.InverseTransformPoint(world).y;
        }

        void ResolveRefs()
        {
            if (_tower == null)
                _tower = UnityObjectFinder.FindFirst<TowerManager>();
            if (_climb == null)
                _climb = UnityObjectFinder.FindFirst<ClimbController>();
        }

        void SetPanelVisible(bool visible)
        {
            if (panelRoot != null && panelRoot.activeSelf != visible)
                panelRoot.SetActive(visible);
        }

        void RefreshAvatarSpriteIfNeeded()
        {
            if (avatarPortraitImage == null || SaveService.Data == null)
                return;

            var id = SaveService.Data.equippedCharacterId ?? string.Empty;
            if (string.Equals(_lastPortraitCharacterId, id, System.StringComparison.Ordinal))
                return;

            _lastPortraitCharacterId = id;

            Sprite spr = null;
            var db = ConfigRegistry.Characters;
            if (db != null && db.TryGet(id, out var def) && def != null)
                spr = def.climbTowerProgressAvatar;

            avatarPortraitImage.sprite = spr;
            avatarPortraitImage.enabled = spr != null;
        }
    }
}
