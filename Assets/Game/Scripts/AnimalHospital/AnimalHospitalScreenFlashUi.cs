using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 全屏白闪（相机交互）。请在 HUD 下自行放置全屏 Image，并拖到 Flash Image。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalScreenFlashUi : MonoBehaviour
    {
        [SerializeField] private Image flashImage;
        [SerializeField, Min(0.01f)] private float peakHoldSeconds = 0.05f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.35f;

        Coroutine _routine;

        public Image FlashImage => flashImage;

        void Awake()
        {
            if (flashImage == null)
                flashImage = GetComponent<Image>();
            if (flashImage != null)
            {
                var c = flashImage.color;
                c.a = 0f;
                flashImage.color = c;
            }

            gameObject.SetActive(false);
        }

        public void PlayFlash()
        {
            if (flashImage == null)
            {
                Debug.LogWarning("[AH] ScreenFlashUi 未配置 Flash Image，跳过白闪。", this);
                return;
            }

            gameObject.SetActive(true);
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            var c = flashImage.color;
            c.a = 1f;
            flashImage.color = c;
            yield return new WaitForSeconds(peakHoldSeconds);

            var elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                var t = fadeSeconds > 0f ? Mathf.Clamp01(elapsed / fadeSeconds) : 1f;
                c.a = Mathf.Lerp(1f, 0f, t);
                flashImage.color = c;
                yield return null;
            }

            c.a = 0f;
            flashImage.color = c;
            _routine = null;
            gameObject.SetActive(false);
        }
    }
}
