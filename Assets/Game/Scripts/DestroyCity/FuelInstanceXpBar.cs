using UnityEngine;
using UnityEngine.UI;

namespace Game.DestroyCity
{
    /// <summary>燃料实例世界空间经验进度条（挂在燃料预制体的 Canvas 下）。</summary>
    [DisallowMultipleComponent]
    public sealed class FuelInstanceXpBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Slider slider;
        [Header("文本")]
        [SerializeField] private Text levelText;
        [SerializeField] private Text xpText;
        [SerializeField] private string levelFormat = "Lv.{0}";
        [SerializeField] private string xpFormat = "{0}/{1}";
        [SerializeField] private string maxLevelXpText = "MAX";
        [Header("动效")]
        [SerializeField] private bool animateFill = true;
        [SerializeField] private float fillLerpSpeed = 8f;

        float _displayFill;
        float _targetFill;
        bool _animating;

        void Awake()
        {
            if (fillImage == null)
                fillImage = transform.Find("Fill")?.GetComponent<Image>();
            if (slider == null)
                slider = GetComponentInChildren<Slider>(true);
            if (levelText == null)
                levelText = FindText("LevelText", "Level", "等级");
            if (xpText == null)
                xpText = FindText("XpText", "XpValue", "ValueText", "经验值");

            _displayFill = ReadCurrentFill();
            _targetFill = _displayFill;
            ApplyFill(_displayFill);
        }

        Text FindText(params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var tr = transform.Find(names[i]);
                if (tr == null)
                    continue;
                var text = tr.GetComponent<Text>();
                if (text != null)
                    return text;
            }

            return null;
        }

        void Update()
        {
            if (!_animating)
                return;

            _displayFill = Mathf.MoveTowards(_displayFill, _targetFill, fillLerpSpeed * Time.deltaTime);
            ApplyFill(_displayFill);
            if (Mathf.Approximately(_displayFill, _targetFill))
                _animating = false;
        }

        public void Refresh(int level, float xp, bool animate = true)
        {
            _targetFill = ComputeFill01(level, xp);
            ApplyTexts(level, xp);

            if (!animate || !isActiveAndEnabled)
            {
                _displayFill = _targetFill;
                ApplyFill(_displayFill);
                _animating = false;
                return;
            }

            _animating = true;
        }

        public static float ComputeFill01(int level, float xp)
        {
            level = Mathf.Max(1, level);
            xp = Mathf.Max(0f, xp);
            var maxLevel = FuelXpMath.GetMaxLevel();
            if (level >= maxLevel)
                return 1f;

            var need = FuelXpMath.GetXpToNextLevel(level);
            if (need <= 0f)
                return 1f;

            return Mathf.Clamp01(xp / need);
        }

        void ApplyTexts(int level, float xp)
        {
            level = Mathf.Max(1, level);
            xp = Mathf.Max(0f, xp);

            if (levelText != null)
                levelText.text = string.Format(levelFormat, level);

            if (xpText == null)
                return;

            var maxLevel = FuelXpMath.GetMaxLevel();
            if (level >= maxLevel)
            {
                xpText.text = maxLevelXpText;
                return;
            }

            var need = FuelXpMath.GetXpToNextLevel(level);
            xpText.text = string.Format(xpFormat, FormatXp(xp), FormatXp(need));
        }

        static string FormatXp(float value)
        {
            if (value >= 1_000_000f)
                return (value / 1_000_000f).ToString("0.#") + "M";
            if (value >= 10_000f)
                return (value / 1_000f).ToString("0.#") + "K";
            return Mathf.RoundToInt(value).ToString();
        }

        float ReadCurrentFill()
        {
            if (fillImage != null)
                return fillImage.fillAmount;
            if (slider != null)
                return slider.value;
            return 0f;
        }

        void ApplyFill(float value)
        {
            value = Mathf.Clamp01(value);
            if (fillImage != null)
                fillImage.fillAmount = value;
            if (slider != null)
                slider.value = value;
        }
    }
}
