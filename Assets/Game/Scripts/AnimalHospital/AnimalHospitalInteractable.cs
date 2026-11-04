using System;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>可交互物体基类：准星对准后需按住交互键完成进度。</summary>
    [DisallowMultipleComponent]
    public class AnimalHospitalInteractable : MonoBehaviour
    {
        [SerializeField] private AnimalHospitalInteractableKind kind;
        [SerializeField] private string interactableId;
        [SerializeField] private bool interactable = true;
        [SerializeField] private float holdSecondsOverride = -1f;
        [SerializeField] private string displayName;

        public AnimalHospitalInteractableKind Kind => kind;
        public string InteractableId => interactableId;
        public virtual string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? kind.ToString() : displayName;
        public bool IsInteractable => interactable && isActiveAndEnabled;
        public float HoldSecondsOverride => holdSecondsOverride;
        public bool GuideHighlighted { get; private set; }

        public event Action<AnimalHospitalInteractable> InteractionCompleted;

        public void Configure(AnimalHospitalInteractableKind interactKind, string id = null, string name = null)
        {
            kind = interactKind;
            if (!string.IsNullOrWhiteSpace(id))
                interactableId = id;
            if (!string.IsNullOrWhiteSpace(name))
                displayName = name;
        }

        public void SetInteractable(bool enabled) => interactable = enabled;

        public float ResolveHoldSeconds(Config.AnimalHospitalConfig config)
        {
            if (holdSecondsOverride > 0f)
                return holdSecondsOverride;
            return config != null ? config.ResolveHoldSeconds(kind) : 1f;
        }

        public virtual bool CanInteract(AnimalHospitalService service) => IsInteractable;

        public virtual void OnInteractionCompleted(AnimalHospitalService service)
        {
            InteractionCompleted?.Invoke(this);
            service?.HandleInteractableCompleted(this);
        }

        public void SetGuideHighlighted(bool highlighted) => GuideHighlighted = highlighted;
    }
}
