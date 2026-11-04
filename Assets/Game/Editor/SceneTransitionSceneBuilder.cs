#if UNITY_EDITOR
using Game.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// 生成 WebGL1 可用过场场景：仅 UGUI + 内置 UI 默认材质，无自定义 Shader。
    /// </summary>
    public static class SceneTransitionSceneBuilder
    {
        const string ScenePath = Game.Core.Content.GameContentPaths.SceneTransitionScenePath;

        [MenuItem("Game/Maps/Rebuild SceneTransition Scene (no confirm)", false, 101)]
        public static void RebuildNoConfirm()
        {
            RebuildCore();
        }

        [MenuItem("Game/Maps/Rebuild SceneTransition Scene")]
        public static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild SceneTransition",
                    "将清空并覆盖「SceneTransition」场景为最小过场配置（Main Camera、EventSystem、TransitionOverlayBinding UI）。\n是否继续？",
                    "确定",
                    "取消"))
                return;

            RebuildCore();
        }

        static void RebuildCore()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            BuildEventSystem();
            BuildTransitionController();
            BuildTransitionUiCanvas();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log(
                $"[{nameof(SceneTransitionSceneBuilder)}] 已保存 {ScenePath}。请确认 Build Settings 中包含该场景；运行一次 Play 测试跳转。");
        }

        static void BuildEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        static void BuildTransitionController()
        {
            var go = new GameObject("TransitionRuntime");
            go.AddComponent<TransitionSceneController>();
            go.AddComponent<TransitionScenePresenter>();
            Undo.RegisterCreatedObjectUndo(go, "Create TransitionRuntime");
        }

        /// <summary>内置 UI 圆片，用作 Mask 形状（UI/Default，WebGL1 可用）。</summary>
        static Sprite BuiltinKnobSprite()
        {
            var s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (s != null)
                return s;

            var tex = Texture2D.whiteTexture;
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        static Sprite WhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        static void BuildTransitionUiCanvas()
        {
            var root = new GameObject("TransitionUICanvas");
            root.layer = 5;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            var cg = root.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var binding = root.AddComponent<TransitionOverlayBinding>();

            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = Vector2.zero;
            rootRt.localScale = Vector3.one;

            var maskGo = new GameObject("Mask");
            maskGo.layer = 5;
            maskGo.transform.SetParent(root.transform, false);
            var maskRt = maskGo.AddComponent<RectTransform>();
            StretchFull(maskRt);
            maskRt.localScale = Vector3.one;

            var maskImg = maskGo.AddComponent<Image>();
            maskImg.sprite = BuiltinKnobSprite();
            maskImg.color = Color.white;
            maskImg.type = Image.Type.Simple;
            maskImg.raycastTarget = true;

            var mask = maskGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var bgGo = new GameObject("BG");
            bgGo.layer = 5;
            bgGo.transform.SetParent(maskGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            StretchFull(bgRt);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = WhiteSprite();
            bgImg.color = Color.black;
            bgImg.type = Image.Type.Simple;
            bgImg.raycastTarget = true;

            BuildProgressPanel(root.transform);

            var so = new SerializedObject(binding);
            so.FindProperty("rootCanvasGroup").objectReferenceValue = cg;
            so.FindProperty("maskScaleRoot").objectReferenceValue = maskRt;
            so.FindProperty("maskGraphic").objectReferenceValue = maskImg;
            so.FindProperty("backgroundImage").objectReferenceValue = bgImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Create TransitionUICanvas");
        }

        static void BuildProgressPanel(Transform canvasRoot)
        {
            var panelGo = new GameObject("ProgressPanel");
            panelGo.layer = 5;
            panelGo.transform.SetParent(canvasRoot, false);
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0f);
            panelRt.anchorMax = new Vector2(0.5f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.sizeDelta = new Vector2(640f, 120f);
            panelRt.anchoredPosition = new Vector2(0f, 80f);

            var statusGo = new GameObject("StatusText");
            statusGo.layer = 5;
            statusGo.transform.SetParent(panelGo.transform, false);
            var statusRt = statusGo.AddComponent<RectTransform>();
            StretchFull(statusRt);
            statusRt.offsetMin = new Vector2(0f, 48f);
            statusRt.offsetMax = Vector2.zero;
            var statusText = statusGo.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            statusText.fontSize = 28;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = Color.white;
            statusText.text = string.Empty;

            var sliderGo = new GameObject("ProgressSlider");
            sliderGo.layer = 5;
            sliderGo.transform.SetParent(panelGo.transform, false);
            var sliderRt = sliderGo.AddComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.05f, 0.15f);
            sliderRt.anchorMax = new Vector2(0.95f, 0.35f);
            sliderRt.offsetMin = Vector2.zero;
            sliderRt.offsetMax = Vector2.zero;
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;

            var bgGo = new GameObject("Background");
            bgGo.layer = 5;
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            StretchFull(bgRt);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = WhiteSprite();
            bgImg.color = new Color(1f, 1f, 1f, 0.25f);
            slider.targetGraphic = bgImg;

            var fillArea = new GameObject("Fill Area");
            fillArea.layer = 5;
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            StretchFull(fillAreaRt);
            fillAreaRt.offsetMin = new Vector2(8f, 8f);
            fillAreaRt.offsetMax = new Vector2(-8f, -8f);

            var fillGo = new GameObject("Fill");
            fillGo.layer = 5;
            fillGo.transform.SetParent(fillArea.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            StretchFull(fillRt);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.sprite = WhiteSprite();
            fillImg.color = new Color(0.2f, 0.85f, 0.35f, 1f);
            slider.fillRect = fillRt;

            panelGo.SetActive(false);

            var presenter = UnityEngine.Object.FindObjectOfType<TransitionScenePresenter>();

            if (presenter != null)
            {
                var presenterSo = new SerializedObject(presenter);
                presenterSo.FindProperty("progressRoot").objectReferenceValue = panelGo;
                presenterSo.FindProperty("progressSlider").objectReferenceValue = slider;
                presenterSo.FindProperty("statusText").objectReferenceValue = statusText;
                presenterSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
        }
    }
}
#endif
