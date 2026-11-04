using System.Collections;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>入院单生成后的短位移弹出动画。</summary>
    public static class AnimalHospitalSpawnMotion
    {
        public static void PlayPopIn(
            MonoBehaviour host,
            Transform target,
            Vector3 endPosition,
            float duration = 0.45f,
            Vector3 startOffset = default)
        {
            if (host == null || target == null)
                return;

            if (startOffset == default)
                startOffset = new Vector3(0f, 0.55f, -0.15f);

            host.StartCoroutine(PopInRoutine(target, endPosition, duration, startOffset));
        }

        static IEnumerator PopInRoutine(
            Transform target,
            Vector3 endPosition,
            float duration,
            Vector3 startOffset)
        {
            var start = endPosition + startOffset;
            target.position = start;
            var elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                target.position = Vector3.Lerp(start, endPosition, eased);
                yield return null;
            }

            target.position = endPosition;
        }
    }
}
