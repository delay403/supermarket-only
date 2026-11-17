using Game.Audio;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeSkillSlotUi : MonoBehaviour
    {
        [SerializeField] private SupermarketSkillId skillId;
        [SerializeField] private Button useButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image cooldownFillImage;
        [SerializeField] private Text cooldownText;
        [SerializeField] private GameObject lockedRoot;
        [SerializeField] private Button lockButton;
        [SerializeField] private Text hotkeyText;

        SupermarketOrganizeSkillController _controller;
        int _hotkeyNumber;

        public SupermarketSkillId SkillId => skillId;

        public void Configure(
            SupermarketSkillId id,
            Button button,
            Image icon,
            Image cooldownFill,
            Text cooldown,
            GameObject locked,
            Button lockedButton = null)
        {
            skillId = id;
            useButton = button;
            iconImage = icon;
            cooldownFillImage = cooldownFill;
            cooldownText = cooldown;
            lockedRoot = locked;
            lockButton = lockedButton ?? locked?.GetComponent<Button>();
            EnsureCooldownFillSettings();
        }

        public void Bind(SupermarketOrganizeSkillController controller)
        {
            _controller = controller;
            RefreshVisual();
        }

        void Awake()
        {
            ResolveLockButtonReference();
            EnsureCooldownFillSettings();
            useButton?.onClick.AddListener(OnUseClicked);
            WireLockButton(true);
        }

        void OnDestroy()
        {
            useButton?.onClick.RemoveListener(OnUseClicked);
            WireLockButton(false);
        }

        void OnUseClicked()
        {
            _controller?.TryUseSkill(skillId);
        }

        public void UseFromHotkey()
        {
            _controller?.TryUseSkill(skillId);
        }

        public void SetHotkeyNumber(int number)
        {
            _hotkeyNumber = number;
            EnsureHotkeyLabel();
        }

        void EnsureHotkeyLabel()
        {
            if (_hotkeyNumber <= 0)
                return;

            if (hotkeyText == null)
            {
                var existing = transform.Find("HotkeyLabel");
                if (existing != null)
                    hotkeyText = existing.GetComponent<Text>();
                if (hotkeyText == null)
                    hotkeyText = CreateHotkeyLabel();
            }

            if (hotkeyText != null)
                hotkeyText.text = _hotkeyNumber.ToString();
        }

        Text CreateHotkeyLabel()
        {
            var go = new GameObject("HotkeyLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(transform, false);
            go.transform.SetAsLastSibling();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(3f, -2f);
            rect.sizeDelta = new Vector2(24f, 22f);

            var text = go.GetComponent<Text>();
            var font = Resources.Load<Font>("喜脉体");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.font = font;
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;

            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        void OnLockClicked()
        {
            if (SupermarketOrganizeProgressionService.GetSkillLevel(skillId) > 0)
                return;

            AudioGameplayFeedback.PlayUiButton();
        }

        void ResolveLockButtonReference()
        {
            if (lockButton == null && lockedRoot != null)
                lockButton = lockedRoot.GetComponent<Button>();
        }

        void WireLockButton(bool wire)
        {
            if (lockButton == null)
                return;

            if (wire)
                lockButton.onClick.AddListener(OnLockClicked);
            else
                lockButton.onClick.RemoveListener(OnLockClicked);
        }

        public void RefreshVisual()
        {
            RefreshLockState();
            RefreshCooldownVisual();
        }

        void RefreshLockState()
        {
            var unlocked = SupermarketOrganizeProgressionService.GetSkillLevel(skillId) > 0;
            if (lockedRoot != null)
                lockedRoot.SetActive(!unlocked);

            if (lockButton != null)
            {
                lockButton.gameObject.SetActive(!unlocked);
                lockButton.interactable = !unlocked;
            }

            if (iconImage != null)
                iconImage.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.45f);

            if (useButton != null && !unlocked)
                useButton.interactable = false;
        }

        public void RefreshCooldownVisual()
        {
            var unlocked = SupermarketOrganizeProgressionService.GetSkillLevel(skillId) > 0;
            if (!unlocked || _controller == null)
            {
                SetFillAmount(0f);
                SetCooldownText(null);
                if (useButton != null)
                    useButton.interactable = false;
                return;
            }

            var remaining = _controller.GetCooldownRemaining(skillId);
            var max = SupermarketOrganizeProgressionService.GetSkillCooldownSeconds(skillId);
            var fill = max > 0.001f ? Mathf.Clamp01(remaining / max) : 0f;
            SetFillAmount(fill);
            SetCooldownText(remaining > 0.05f ? FormatCooldown(remaining) : null);

            if (useButton != null)
                useButton.interactable = remaining <= 0f;
        }

        void SetFillAmount(float amount)
        {
            if (cooldownFillImage == null)
                return;

            cooldownFillImage.fillAmount = amount;
            cooldownFillImage.enabled = amount > 0.001f;
        }

        void SetCooldownText(string text)
        {
            if (cooldownText == null)
                return;

            var hasText = !string.IsNullOrEmpty(text);
            cooldownText.gameObject.SetActive(hasText);
            if (hasText)
                cooldownText.text = text;
        }

        static string FormatCooldown(float seconds)
        {
            if (seconds >= 60f)
            {
                var minutes = Mathf.FloorToInt(seconds / 60f);
                var secs = Mathf.CeilToInt(seconds - minutes * 60f);
                return $"{minutes:00}:{secs:00}";
            }

            return Mathf.CeilToInt(seconds).ToString();
        }

        static void EnsureCooldownFillSettings(Image fillImage)
        {
            if (fillImage == null)
                return;

            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = true;
            fillImage.raycastTarget = false;
        }

        void EnsureCooldownFillSettings() => EnsureCooldownFillSettings(cooldownFillImage);
    }
}
