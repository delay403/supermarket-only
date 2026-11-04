using UnityEngine;

namespace Game.Economy
{
    /// <summary>
    /// 已合并至 <see cref="DiscountOfferEventManager"/>；保留空壳仅供旧场景序列化迁移。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public sealed class DiscountOfferRuntime : MonoBehaviour
    {
        void Awake()
        {
            if (GetComponent<DiscountOfferEventManager>() == null)
                gameObject.AddComponent<DiscountOfferEventManager>();
        }
    }
}
