using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    [DisallowMultipleComponent]
    public sealed class SupermarketAimHintUi : MonoBehaviour
    {
        [SerializeField] private Text hintText;
        [SerializeField] private GameObject root;
        [SerializeField] private Color defaultHintColor = Color.white;

        Color _savedDefaultColor;

        void Awake()
        {
            if (hintText != null)
                _savedDefaultColor = hintText.color;
            else
                _savedDefaultColor = defaultHintColor;
        }

        public void ApplyAimState(SupermarketAimState state)
        {
            var visible = !string.IsNullOrWhiteSpace(state.hintText);
            if (root != null)
                root.SetActive(visible);
            else if (hintText != null)
                hintText.gameObject.SetActive(visible);

            if (hintText == null)
                return;

            hintText.text = state.hintText ?? string.Empty;
            hintText.color = state.useHintTextColor ? state.hintTextColor : _savedDefaultColor;
        }
    }
}
