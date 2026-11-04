using Game.Visual;
using UnityEngine;

namespace Game.AnimalHospital
{
    public sealed class AnimalHospitalPropInteractable : AnimalHospitalInteractable
    {
        [SerializeField] private AnimalHospitalPropId propId = AnimalHospitalPropId.StunBaton;
        [SerializeField] private string propDisplayName;

        [Header("Hand Visual")]
        [Tooltip("手持模型来源；留空则使用第一个带 Renderer 的子物体。")]
        [SerializeField] private Transform handVisualSource;
        [SerializeField] private GameObject handVisualPrefab;
        [SerializeField] private Vector3 handLocalPosition = new Vector3(0.05f, -0.05f, 0f);
        [SerializeField] private Vector3 handLocalEulerAngles;
        [SerializeField] private Vector3 handLocalScale = Vector3.one;

        public AnimalHospitalPropId PropId => propId;

        void Awake()
        {
            var display = string.IsNullOrWhiteSpace(propDisplayName) ? propId.ToString() : propDisplayName;
            Configure(AnimalHospitalInteractableKind.Prop, propId.ToString(), display);
        }

        public GameObject CreateHandVisual(Transform handleZone)
        {
            if (handleZone == null)
                return null;

            GameObject visualRoot;
            GameObject sourceRoot = null;
            if (handVisualPrefab != null)
            {
                visualRoot = Instantiate(handVisualPrefab, handleZone);
            }
            else
            {
                var source = ResolveHandVisualSource();
                if (source == null)
                    return null;

                sourceRoot = source.gameObject;
                visualRoot = Instantiate(sourceRoot, handleZone);
            }

            visualRoot.name = $"{propId}_HandVisual";
            var visualTransform = visualRoot.transform;
            visualTransform.localPosition = handLocalPosition;
            visualTransform.localRotation = Quaternion.Euler(handLocalEulerAngles);
            visualTransform.localScale = handLocalScale;

            DisablePhysicsAndInteraction(visualRoot);
            InteractableEffectVisualUtility.PrepareHandVisualFromSource(sourceRoot, visualRoot);
            return visualRoot;
        }

        Transform ResolveHandVisualSource()
        {
            if (handVisualSource != null)
                return handVisualSource;

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null)
                    continue;
                if (child.GetComponentInChildren<Renderer>(true) != null)
                    return child;
            }

            return transform;
        }

        static void DisablePhysicsAndInteraction(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            var interactables = root.GetComponentsInChildren<AnimalHospitalInteractable>(true);
            for (var i = 0; i < interactables.Length; i++)
                interactables[i].enabled = false;
        }
    }
}
