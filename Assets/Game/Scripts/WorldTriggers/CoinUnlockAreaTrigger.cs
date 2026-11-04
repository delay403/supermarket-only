using Game.Audio;
using Game.Economy;
using Game.Player;
using Game.Save;
using UnityEngine;

namespace Game.WorldTriggers
{
    /// <summary>
    /// 玩家进入触发器：金币足够则扣除、记录解锁、隐藏自身并播放区域开启音效。
    /// 请在 Inspector 填写唯一的 <see cref="saveKey"/> 以跨会话保持已解锁状态。
    /// </summary>
    public sealed class CoinUnlockAreaTrigger : MonoBehaviour
    {
        [SerializeField] private long costCoins = 1000L;
        [SerializeField] private string saveKey = "area_unlock_default";
        [SerializeField] private LayerMask playerMask = ~0;
        [SerializeField] private bool hideOnSuccess = true;
        [SerializeField] private GameObject[] hideTargets;

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null)
                c.isTrigger = true;
        }

        void Start()
        {
            if (IsAlreadyUnlocked())
                HideTriggerVisuals();
        }

        void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerMask) == 0)
                return;
            if (other.GetComponentInParent<PlayerMotor>() == null)
                return;

            TryUnlock();
        }

        void TryUnlock()
        {
            if (IsAlreadyUnlocked())
            {
                HideTriggerVisuals();
                return;
            }

            var data = SaveService.Data;
            if (data == null || costCoins <= 0)
                return;

            if (data.coins < costCoins)
                return;

            data.coins -= costCoins;
            data.UnlockWorldArea(saveKey);
            SaveService.Save();
            EconomyDisplayEvents.Raise();
            DiscountOfferService.NotifyWorldAreaUnlocked(saveKey);

            AudioGameplayFeedback.PlayResourceDeduct();
            AudioGameplayFeedback.PlayAreaUnlock();

            HideTriggerVisuals();
        }

        bool IsAlreadyUnlocked()
        {
            if (string.IsNullOrWhiteSpace(saveKey))
                return false;
            return SaveService.Data != null && SaveService.Data.HasUnlockedWorldArea(saveKey);
        }

        void HideTriggerVisuals()
        {
            if (!hideOnSuccess)
                return;

            if (hideTargets != null && hideTargets.Length > 0)
            {
                for (var i = 0; i < hideTargets.Length; i++)
                {
                    var target = hideTargets[i];
                    if (target != null)
                        target.SetActive(false);
                }
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
