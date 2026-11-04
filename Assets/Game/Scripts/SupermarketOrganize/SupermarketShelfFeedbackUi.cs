using Game.SupermarketOrganize.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>货架匹配提示 UI：正确/错误货架，以及同 ID 已放置时的额外提示。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketShelfFeedbackUi : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text messageText;
        [SerializeField] private GameObject extraTipRoot;
        [SerializeField] private Color defaultTextColor = Color.white;

        SupermarketOrganizeConfig _config;
        Color _defaultTextColor;

        void Awake()
        {
            if (messageText != null)
                _defaultTextColor = messageText.color;
            else
                _defaultTextColor = defaultTextColor;
        }

        public void ShowShelfMatch(bool isCorrect, bool showExtraTip)
        {
            _config ??= SupermarketOrganizeConfig.ResolveRuntime();

            if (root != null)
                root.SetActive(true);

            if (messageText != null)
            {
                messageText.text = ResolveShelfMessage(isCorrect);
                messageText.color = ResolveShelfTextColor(isCorrect);
            }

            if (extraTipRoot != null)
                extraTipRoot.SetActive(showExtraTip);
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
            if (extraTipRoot != null)
                extraTipRoot.SetActive(false);

            if (messageText != null)
                messageText.color = _defaultTextColor;
        }

        string ResolveShelfMessage(bool isCorrect)
        {
            if (_config == null)
                return isCorrect ? "正确的货架" : "错误的货架";

            if (isCorrect)
            {
                return string.IsNullOrWhiteSpace(_config.correctShelfMessage)
                    ? "正确的货架"
                    : _config.correctShelfMessage;
            }

            return string.IsNullOrWhiteSpace(_config.wrongShelfMessage)
                ? "错误的货架"
                : _config.wrongShelfMessage;
        }

        Color ResolveShelfTextColor(bool isCorrect)
        {
            if (_config == null)
                return _defaultTextColor;

            return isCorrect ? _config.correctShelfTextColor : _config.wrongShelfTextColor;
        }
    }
}
