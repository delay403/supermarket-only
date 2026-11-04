using Game.Core;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    [DisallowMultipleComponent]
    public sealed class PlayerSupermarketOrganizeController : MonoBehaviour
    {
        void Awake()
        {
            SupermarketOrganizePlayerSetup.ConfigurePlayer(gameObject);
        }

        void OnEnable()
        {
            if (GameProductSettings.IsSupermarketOrganizeContentEnabled)
                SupermarketOrganizePlayerSetup.ConfigurePlayer(gameObject);
        }
    }
}
