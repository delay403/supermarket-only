using Game.Core;
using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>挂在玩家根节点，标识当前为躲避虫子模式。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerWormDodgeController : MonoBehaviour
    {
        void Awake()
        {
            WormDodgePlayerSetup.ConfigurePlayer(gameObject);
        }

        void OnEnable()
        {
            if (GameProductSettings.IsWormDodgeContentEnabled)
                WormDodgePlayerSetup.ConfigurePlayer(gameObject);
        }
    }
}
