using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class WorldSpace3DCanvas : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] bool enforceWorldSpace = true;
        [SerializeField] bool enableBillboard = true;
        [FormerlySerializedAs("playerTarget")]
        [SerializeField] Transform cameraTarget;
        [SerializeField] Vector3 lookAtWorldOffset;
        [SerializeField] float minRotateDistance = 0.05f;
        [SerializeField] Text label;
        [SerializeField] string displayText = "";
        [SerializeField] Color solidColor = Color.white;

        Transform _resolvedCamera;

        void Awake()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (enforceWorldSpace && canvas != null) canvas.renderMode = RenderMode.WorldSpace;
            EnsureCanvasInputReady(canvas);
            if (label == null) label = GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = displayText;
                label.color = solidColor;
            }
        }

        void LateUpdate()
        {
            if (!enableBillboard)
                return;
            var target = cameraTarget != null ? cameraTarget : ResolveMainCamera();
            if (target == null)
                return;
            var delta = target.position + lookAtWorldOffset - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude >= minRotateDistance * minRotateDistance)
                transform.rotation = Quaternion.LookRotation(delta, Vector3.up);
        }

        Transform ResolveMainCamera()
        {
            if (_resolvedCamera == null && Camera.main != null)
                _resolvedCamera = Camera.main.transform;
            return _resolvedCamera;
        }

        public static void EnsureCanvasInputReady(Canvas target)
        {
            if (target == null)
                return;
            if (target.renderMode == RenderMode.WorldSpace && target.worldCamera == null)
                target.worldCamera = Camera.main;
            if (target.GetComponent<GraphicRaycaster>() == null)
                target.gameObject.AddComponent<GraphicRaycaster>();
        }

        public void SetDisplayText(string text)
        {
            displayText = text ?? string.Empty;
            if (label != null) label.text = displayText;
        }

        public void SetSolidColor(Color color)
        {
            solidColor = color;
            if (label != null) label.color = color;
        }

        public void SetCameraTarget(Transform target)
        {
            cameraTarget = target;
            _resolvedCamera = target;
        }
    }
}
