using Game.Collectibles.Stats;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 摧毁城市玩家根组件（对标 <see cref="Fishing.PlayerFishingController"/>）。
    /// 负责禁用钓鱼组件、装配射线交互与燃料携带。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerDestroyCityController : MonoBehaviour
    {
        [SerializeField] private DestroyCityStatsAggregator statsAggregator;

        void Awake()
        {
            if (!GameProductSettings.IsDestroyCityContentEnabled)
            {
                enabled = false;
                return;
            }

            if (statsAggregator == null)
                statsAggregator = GetComponent<DestroyCityStatsAggregator>();

            DestroyCityPlayerSetup.ConfigurePlayer(gameObject);
        }
    }
}
