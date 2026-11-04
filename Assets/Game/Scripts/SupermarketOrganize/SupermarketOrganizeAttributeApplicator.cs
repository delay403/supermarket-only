using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>将属性等级应用到拾取范围与手持容量。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeAttributeApplicator : MonoBehaviour
    {
        [SerializeField] private SupermarketCrosshairInteractor crosshair;
        [SerializeField] private SupermarketOrganizeService service;

        void OnEnable()
        {
            SupermarketOrganizeProgressionService.AttributeUpgraded += OnProgressionChanged;
            ApplyAll();
        }

        void OnDisable()
        {
            SupermarketOrganizeProgressionService.AttributeUpgraded -= OnProgressionChanged;
        }

        public void Configure(
            SupermarketOrganizeService organizeService,
            SupermarketCrosshairInteractor interactor)
        {
            service = organizeService;
            crosshair = interactor;
            ApplyAll();
        }

        void OnProgressionChanged(SupermarketAttributeId attributeId, int level) => ApplyAll();

        public void ApplyAll()
        {
            crosshair?.ApplyPickupRange(SupermarketOrganizeProgressionService.GetPickupRange());
            service?.ApplyProgressionAttributes();
        }
    }
}
