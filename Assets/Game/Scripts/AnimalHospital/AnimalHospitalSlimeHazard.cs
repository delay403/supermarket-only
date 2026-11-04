using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>走廊粘液：持续放大并损伤理智；冷冻枪可消除。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalSlimeHazard : MonoBehaviour
    {
        [SerializeField] private float growSpeed = 0.05f;
        [SerializeField] private float contactRadius = 1.2f;

        AnimalHospitalService _service;
        float _life;
        float _duration = 180f;
        float _sanityTick;

        public void Initialize(AnimalHospitalService service, float duration, float grow)
        {
            _service = service;
            _duration = duration;
            growSpeed = grow;
            var interactable = GetComponent<AnimalHospitalInteractable>()
                               ?? gameObject.AddComponent<AnimalHospitalInteractable>();
            interactable.Configure(AnimalHospitalInteractableKind.Slime, "slime", "粘液");
        }

        void Update()
        {
            if (_service == null)
                return;

            _life += Time.deltaTime;
            transform.localScale += Vector3.one * (growSpeed * Time.deltaTime);

            var player = _service.PlayerTransform;
            if (player != null)
            {
                var radius = contactRadius * Mathf.Max(transform.localScale.x, 1f);
                if (Vector3.Distance(transform.position, player.position) <= radius)
                {
                    _sanityTick += Time.deltaTime;
                    if (_sanityTick >= 1f)
                    {
                        _sanityTick = 0f;
                        var loss = _service.Config != null
                            ? _service.Config.corridorSlimeSanityLossPerSecond
                            : 5f;
                        _service.Sanity.ApplyDamage(loss);
                    }
                }
            }

            if (_life >= _duration)
            {
                _service.NotifySlimeExpired(this);
                Destroy(gameObject);
            }
        }
    }
}
