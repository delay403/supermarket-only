using Game.Player;
using Game.UI.Framework;
using UnityEngine;

namespace Game.WorldTriggers
{
    /// <summary>
    /// 进入触发器打开指定 UGUI（与 FGUI 路由按钮一致）。请在场景中放在 wings shop / 地图选择等入口旁。
    /// </summary>
    public sealed class ShopOpenTrigger : MonoBehaviour
    {
        [SerializeField] private UiPanelId panel = UiPanelId.WingShop;
        [SerializeField] private LayerMask playerMask = ~0;

        [Header("Pet Hatch")]
        [Tooltip("仅当 panel = PetHatch 时生效：与 PetHatchPoolConfig.poolId 一致（推荐，不易因目录顺序变化出错）。")]
        [SerializeField] private string petHatchPoolId;

        [Tooltip("仅当 panel = PetHatch 且上一项为空时生效：PetHatchPoolsCatalog 中第几个卡池（0 起）。")]
        [SerializeField] private int petHatchCatalogIndex = -1;

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerMask) == 0)
                return;
            if (other.GetComponentInParent<PlayerMotor>() == null)
                return;

            if (panel == UiPanelId.None)
                return;

            if (panel == UiPanelId.PetHatch &&
                (!string.IsNullOrWhiteSpace(petHatchPoolId) || petHatchCatalogIndex >= 0))
            {
                UiCoordinator.Open(panel, new UiOpenPayload
                {
                    PetHatch = new PetHatchOpenPayload
                    {
                        PoolId = string.IsNullOrWhiteSpace(petHatchPoolId) ? null : petHatchPoolId.Trim(),
                        CatalogIndex = petHatchCatalogIndex,
                    },
                });
                return;
            }

            UiCoordinator.Open(panel);
        }
    }
}
