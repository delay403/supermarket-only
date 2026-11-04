using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>导弹预测落点标记（下落阶段显示）。</summary>
    [DisallowMultipleComponent]
    public sealed class NukeLandingMarker : MonoBehaviour
    {
        [SerializeField] private float groundOffset = 0.05f;

        public void ShowAt(Vector3 worldPosition)
        {
            var pos = worldPosition;
            pos.y += groundOffset;
            transform.position = pos;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
