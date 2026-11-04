using System;
using Game.Collectibles.Characters;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>躲避虫子主页：解锁角色确认弹窗。</summary>
    public sealed class WormDodgeCharacterUnlockOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button maskButton;

        Action _onConfirm;

        void Awake()
        {
            confirmButton?.onClick.AddListener(OnConfirmClicked);
            closeButton?.onClick.AddListener(Hide);
            maskButton?.onClick.AddListener(Hide);
            HideImmediate();
        }

        public void Show(CharacterShopRowViewModel row, bool canAfford, Action onConfirm)
        {
            if (row == null || root == null)
                return;

            _onConfirm = onConfirm;
            root.SetActive(true);

            if (titleText != null)
                titleText.text = "解锁角色";

            if (messageText != null)
            {
                var currency = row.PriceCurrency == CharacterCurrencyType.Gems ? "钻石" : "金币";
                messageText.text = $"是否消耗 {currency} x{row.PriceAmount} 解锁该角色？";
            }

            if (confirmLabel != null)
                confirmLabel.text = canAfford ? "解锁" : "金币不足";

            if (confirmButton != null)
                confirmButton.interactable = canAfford;
        }

        public void Hide()
        {
            _onConfirm = null;
            if (root != null)
                root.SetActive(false);
        }

        void HideImmediate()
        {
            _onConfirm = null;
            if (root != null)
                root.SetActive(false);
        }

        void OnConfirmClicked()
        {
            var cb = _onConfirm;
            Hide();
            cb?.Invoke();
        }

        public void TryAutoWire(Transform searchRoot)
        {
            if (searchRoot == null)
                return;

            if (root == null)
            {
                var found = searchRoot.Find("UnlockOverlay");
                if (found != null)
                    root = found.gameObject;
            }

            if (root == null)
                return;

            if (titleText == null)
                titleText = FindText(root.transform, "TitleText");
            if (messageText == null)
                messageText = FindText(root.transform, "MessageText");
            if (confirmLabel == null)
                confirmLabel = FindText(root.transform, "ConfirmLabel");
            if (confirmButton == null)
                confirmButton = FindButton(root.transform, "ConfirmButton");
            if (closeButton == null)
                closeButton = FindButton(root.transform, "CloseButton");
            if (maskButton == null)
                maskButton = FindButton(root.transform, "MaskButton");
        }

        static Text FindText(Transform parent, string name)
        {
            var t = parent.Find(name);
            return t != null ? t.GetComponent<Text>() : null;
        }

        static Button FindButton(Transform parent, string name)
        {
            var t = parent.Find(name);
            return t != null ? t.GetComponent<Button>() : null;
        }
    }
}
