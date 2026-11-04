using UnityEngine;

namespace Game.Features.OnlineReward
{
    /// <summary>
    /// 常驻计时：累计在线时长。挂在 Bootstrap / MainHome / HUD 任一常驻物体上即可。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineRewardTicker : MonoBehaviour
    {
        [SerializeField] private bool pauseWhenTimeScaleZero = true;

        void Update()
        {
            if (pauseWhenTimeScaleZero && Time.timeScale <= 0f)
                return;

            OnlineRewardService.Tick(Time.unscaledDeltaTime);
        }
    }
}
