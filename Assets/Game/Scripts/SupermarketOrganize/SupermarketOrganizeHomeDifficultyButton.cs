using System;
using Game.SupermarketOrganize.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>主页难度按钮（场景中手动放置，通过 Entry Id 关联配置）。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeHomeDifficultyButton : MonoBehaviour
    {
        [SerializeField] private string entryId;
        [SerializeField] private Button button;
        [SerializeField] private Image glowImage;
        [SerializeField] private Vector2 detailPopupOffset;
        [SerializeField] private RectTransform entryAnimationTarget;

        SupermarketOrganizeDifficultyEntryDef _entry;
        Action<SupermarketOrganizeHomeDifficultyButton> _onClick;
        Transform _savedParent;
        int _savedSiblingIndex;

        public string EntryId => entryId;
        public SupermarketOrganizeDifficultyEntryDef Entry => _entry;
        public Vector2 DetailPopupOffset => detailPopupOffset;
        public RectTransform RectTransform => transform as RectTransform;

        void Awake()
        {
            if (button == null)
                button = GetComponentInChildren<Button>();
            button?.onClick.AddListener(() => _onClick?.Invoke(this));
            CaptureLayerState();
        }

        public void Bind(
            SupermarketOrganizeDifficultyEntryDef entry,
            Action<SupermarketOrganizeHomeDifficultyButton> onClick)
        {
            _entry = entry;
            _onClick = onClick;
            if (entry != null && !string.IsNullOrWhiteSpace(entry.id))
                entryId = entry.id;
        }

        public void CaptureLayerState()
        {
            _savedParent = transform.parent;
            _savedSiblingIndex = transform.GetSiblingIndex();
        }

        public void RaiseAboveMask(RectTransform insertBefore)
        {
            if (insertBefore != null)
                transform.SetSiblingIndex(insertBefore.GetSiblingIndex());
            else
                transform.SetAsLastSibling();
        }

        public void RestoreLayer()
        {
            if (_savedParent == null)
                return;

            if (transform.parent != _savedParent)
                transform.SetParent(_savedParent, false);

            transform.SetSiblingIndex(_savedSiblingIndex);
        }

        public Vector2 ResolveEntryAnimationTarget(RectTransform relativeToParent)
        {
            var target = entryAnimationTarget != null ? entryAnimationTarget : RectTransform;
            if (target == null || relativeToParent == null)
                return Vector2.zero;

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                relativeToParent,
                target);
            return bounds.center;
        }

        public void SetSelected(bool selected)
        {
            if (glowImage != null)
                glowImage.enabled = selected;
        }

        public void SetLabelsVisible(bool visible)
        {
            if (button == null)
                return;

            var graphics = button.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null || graphic.gameObject == button.gameObject)
                    continue;

                graphic.enabled = visible;
            }
        }
    }
}
