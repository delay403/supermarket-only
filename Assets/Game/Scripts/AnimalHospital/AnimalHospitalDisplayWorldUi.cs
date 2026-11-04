using UnityEngine;
using UnityEngine.UI;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 病房显示屏世界 UI。请自建 World Space Canvas，并拖入 Icon Image / Name Text。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalDisplayWorldUi : MonoBehaviour
    {
        [Header("请自行在场景中创建并引用")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text nameText;
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private bool hideOnAwake = true;

        void Awake()
        {
            if (hideOnAwake)
                SetVisible(false);
        }

        public bool IsConfigured => canvas != null && nameText != null;

        public void ShowMedicine(string displayName, Sprite icon)
        {
            if (!IsConfigured)
            {
                Debug.LogWarning($"[AH] {name} 的 DisplayWorldUi 未配置 Canvas/NameText。", this);
                return;
            }

            SetVisible(true);
            nameText.text = displayName ?? string.Empty;
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            }
        }

        void SetVisible(bool visible)
        {
            if (canvas == null)
                return;

            if (canvas.gameObject == gameObject)
                canvas.enabled = visible;
            else
                canvas.gameObject.SetActive(visible);
        }

        void LateUpdate()
        {
            if (!billboardToCamera || canvas == null || !canvas.isActiveAndEnabled)
                return;
            var cam = Camera.main;
            if (cam == null)
                return;
            var dir = canvas.transform.position - cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                canvas.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}
