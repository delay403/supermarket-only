using UnityEngine;
using UnityEngine.UI;

namespace Game.DestroyCity
{
    /// <summary>核弹发射器世界空间等级 / 经验进度条。</summary>
    [DisallowMultipleComponent]
    public sealed class NukeLauncherXpBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Slider slider;
        [SerializeField] private Text levelText;
        [SerializeField] private Text xpText;
        [SerializeField] private string levelFormat = "导弹 Lv.{0}";
        [SerializeField] private string xpFormat = "{0}/{1}";
        [SerializeField] private string maxLevelXpText = "MAX";
        [SerializeField] private bool animateFill = true;
        [SerializeField] private float fillLerpSpeed = 8f;

        float _displayFill;
        float _targetFill;
        bool _animating;

        public bool IsWired => levelText != null && (slider != null || fillImage != null);

        public int WiringScore
        {
            get
            {
                var score = 0;
                if (levelText != null) score += 4;
                if (slider != null) score += 2;
                if (fillImage != null) score += 2;
                if (xpText != null) score += 1;
                return score;
            }
        }

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

        void OnEnable()
        {
            NukeUpgradeService.NukeUpgraded += OnNukeChanged;
            NukeUpgradeService.NukeXpChanged += OnNukeXpChanged;
            Refresh(false);
        }

        void OnDisable()
        {
            NukeUpgradeService.NukeUpgraded -= OnNukeChanged;
            NukeUpgradeService.NukeXpChanged -= OnNukeXpChanged;
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

        void OnNukeChanged(int _) => Refresh(true);
        void OnNukeXpChanged(float _) => Refresh(true);

        public void Refresh(bool animate = true)
        {
            var level = NukeUpgradeService.GetNukeLevel();
            var xp = NukeUpgradeService.GetNukeXp();
            _targetFill = ComputeFill01(level, xp);
            ApplyTexts(level, xp);

            NukeLauncherDiagnostics.Log(
                $"XpBar.Refresh | go={name} wired={IsWired} animate={animate} " +
                $"Lv{level} xp={xp:F0} fill={_targetFill:F2} " +
                $"levelText={(levelText != null ? levelText.text : "null")} " +
                $"xpText={(xpText != null ? xpText.text : "null")}",
                this);

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
            if (NukeUpgradeService.IsMaxLevel())
                return 1f;

            var need = NukeUpgradeService.GetUpgradeXpRequired();
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

            if (NukeUpgradeService.IsMaxLevel())
            {
                xpText.text = maxLevelXpText;
                return;
            }

            var need = NukeUpgradeService.GetUpgradeXpRequired();
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
