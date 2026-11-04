using Game.Core;
using Game.Player;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizePlayerMotorBoost : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();
        }

        void OnEnable()
        {
            SupermarketOrganizeProgressionService.AttributeUpgraded += OnAttributeUpgraded;
            ApplyStats();
        }

        void OnDisable()
        {
            SupermarketOrganizeProgressionService.AttributeUpgraded -= OnAttributeUpgraded;
            if (motor != null)
                motor.SetSupermarketMoveSpeedBonus(0f);
        }

        void OnAttributeUpgraded(SupermarketAttributeId attributeId, int level)
        {
            if (attributeId == SupermarketAttributeId.MoveSpeed)
                ApplyStats();
        }

        void ApplyStats()
        {
            if (!GameProductSettings.IsSupermarketOrganizeContentEnabled || motor == null)
                return;

            motor.SetSupermarketMoveSpeedBonus(SupermarketOrganizeProgressionService.GetMoveSpeedBonus());
        }
    }
}
