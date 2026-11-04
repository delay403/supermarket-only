using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>通用提示框参数。</summary>
    public sealed class HUDMessage
    {
        public string Title;
        public string Content;
        public bool BtnShow;
        public bool MiddleBtnShow;
        public string ConfirmText = "确认";
        public string CancelText = "取消";
        public bool CloseOnConfirm = true;
        public bool CloseOnCancel = true;
        public Action ConfirmAction;
        public Action CancelAction;
    }

    /// <summary>
    /// 轻量运行时提示框（UGUI）。替代原第三方 HUD 弹窗，支持确认/取消回调。
    /// </summary>
    public static class HUDMgr
    {
        static GameObject s_root;
        static Font s_font;

        public static void ShowHUD(HUDMessage message)
        {
            if (message == null)
                return;

            EnsureEventSystem();
            EnsureRoot();

            var overlay = new GameObject("HUDMessage", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlay.transform.SetParent(s_root.transform, false);

            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            var overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.55f);
            overlayImage.raycastTarget = true;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620f, 340f);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.18f, 0.98f);

            var title = CreateText(panel.transform, "Title", message.Title, 30, new Vector2(0f, 120f), new Vector2(560f, 50f));
            title.fontStyle = FontStyle.Bold;

            var content = CreateText(panel.transform, "Content", message.Content, 24, new Vector2(0f, 20f), new Vector2(560f, 130f));
            content.alignment = TextAnchor.MiddleCenter;

            var showConfirm = message.BtnShow || message.MiddleBtnShow;
            var showCancel = showConfirm && (!string.IsNullOrEmpty(message.CancelText)
                                             && (message.CloseOnCancel || message.CancelAction != null));

            if (showConfirm)
            {
                var confirmObj = CreateButton(panel.transform, "ConfirmButton",
                    string.IsNullOrEmpty(message.ConfirmText) ? "确认" : message.ConfirmText,
                    showCancel ? new Vector2(120f, -120f) : new Vector2(0f, -120f));
                confirmObj.onClick.AddListener(() =>
                {
                    message.ConfirmAction?.Invoke();
                    if (message.CloseOnConfirm)
                        UnityEngine.Object.Destroy(overlay);
                });
            }

            if (showCancel)
            {
                var cancelObj = CreateButton(panel.transform, "CancelButton",
                    string.IsNullOrEmpty(message.CancelText) ? "取消" : message.CancelText,
                    new Vector2(-120f, -120f));
                cancelObj.onClick.AddListener(() =>
                {
                    message.CancelAction?.Invoke();
                    if (message.CloseOnCancel)
                        UnityEngine.Object.Destroy(overlay);
                });
            }
        }

        static void EnsureRoot()
        {
            if (s_root != null)
                return;

            s_root = new GameObject("HUDRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(s_root);
            var canvas = s_root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;

            var scaler = s_root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        static Text CreateText(Transform parent, string name, string value, int size, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;

            var text = go.GetComponent<Text>();
            text.text = value ?? string.Empty;
            text.font = ResolveFont();
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(200f, 64f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.95f, 0.72f, 0.18f, 0.95f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(go.transform, "Label", label, 24, Vector2.zero, new Vector2(190f, 60f));
            text.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            return button;
        }

        static Font ResolveFont()
        {
            if (s_font != null)
                return s_font;

            try
            {
                s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                s_font = null;
            }

            if (s_font == null)
            {
                try
                {
                    s_font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch
                {
                    s_font = null;
                }
            }

            return s_font;
        }
    }
}
