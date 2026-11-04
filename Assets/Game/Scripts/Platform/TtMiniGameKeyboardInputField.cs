using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_WEBGL && WEB_TT
using TTSDK;
#endif

namespace Game.Platform
{
    /// <summary>
    /// 抖音小游戏 WebGL：Unity <c>InputField</c> 需调用 TTSDK 的 <c>TT.ShowKeyboard</c> 才弹出宿主键盘；本组件在点击输入框时拉起键盘并写回文字。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InputField))]
    public sealed class TtMiniGameKeyboardInputField : MonoBehaviour
    {
        [Tooltip("宿主键盘确认键类型：done / next / search / go / send")]
        [SerializeField] private string confirmType = "done";

        [SerializeField] private bool multiple;

        InputField _field;
        bool _clickHooked;

        public static void EnsureOn(InputField field)
        {
#if UNITY_WEBGL && WEB_TT
            if (field == null || field.GetComponent<TtMiniGameKeyboardInputField>() != null)
                return;
            field.gameObject.AddComponent<TtMiniGameKeyboardInputField>();
#endif
        }

        void Awake()
        {
            _field = GetComponent<InputField>();
#if UNITY_WEBGL && WEB_TT
            if (!_clickHooked)
            {
                _clickHooked = true;
                var et = gameObject.GetComponent<EventTrigger>() ?? gameObject.AddComponent<EventTrigger>();
                var e = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                e.callback.AddListener(_ => OnPointerClicked());
                et.triggers.Add(e);
            }
#endif
        }

#if UNITY_WEBGL && WEB_TT
        static TtMiniGameKeyboardInputField s_active;
        static bool s_globalsSubscribed;

        void OnPointerClicked()
        {
            if (_field == null) return;
            s_active = this;
            SubscribeGlobalsIfNeeded();
            _field.Select();
            _field.ActivateInputField();
            StopAllCoroutines();
            StartCoroutine(ShowKeyboardAfterLayout());
        }

        IEnumerator ShowKeyboardAfterLayout()
        {
            yield return null;
            if (_field == null || s_active != this)
                yield break;
            var cap = _field.characterLimit > 0 ? _field.characterLimit : 200;
            var ct = string.IsNullOrWhiteSpace(confirmType) ? "done" : confirmType.Trim();
            TT.ShowKeyboard(
                new TTKeyboard.ShowKeyboardOptions
                {
                    defaultValue = _field.text ?? "",
                    maxLength = cap,
                    multiple = multiple,
                    confirmType = ct,
                },
                () => { },
                err => Debug.LogWarning($"[TtMiniGameKeyboardInputField] ShowKeyboard failed: {err}"));
        }

        static void SubscribeGlobalsIfNeeded()
        {
            if (s_globalsSubscribed)
                return;
            s_globalsSubscribed = true;
            TT.OnKeyboardInput += StaticOnKeyboardInput;
            TT.OnKeyboardConfirm += StaticOnKeyboardConfirm;
            TT.OnKeyboardComplete += StaticOnKeyboardComplete;
        }

        static void StaticOnKeyboardInput(string value)
        {
            if (s_active == null || s_active._field == null) return;
            s_active._field.text = value ?? "";
        }

        static void StaticOnKeyboardConfirm(string value)
        {
            if (s_active == null || s_active._field == null) return;
            s_active._field.text = value ?? "";
        }

        static void StaticOnKeyboardComplete(string value)
        {
            if (s_active != null && s_active._field != null)
            {
                if (!string.IsNullOrEmpty(value))
                    s_active._field.text = value;
                s_active._field.DeactivateInputField();
            }

            s_active = null;
        }

#endif
    }
}
