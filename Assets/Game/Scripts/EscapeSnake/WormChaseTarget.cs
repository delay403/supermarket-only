using System;
using UnityEngine;

namespace Game.EscapeSnake
{
    public enum WormChaseDeathReason
    {
        CaughtByWorm,
        DeathZone,
        Manual
    }

    /// <summary>
    /// 多节虫可追踪的目标：可移动，可被虫体捕获或进入死亡区域后死亡。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormChaseTarget : MonoBehaviour
    {
        [SerializeField] private bool startAlive = true;
        [SerializeField] private bool hideOnDeath = true;

        public bool IsAlive { get; private set; }
        public bool IsInvincible => Time.time < _invincibleUntil;
        public event Action<WormChaseTarget, WormChaseDeathReason> Died;

        float _invincibleUntil = -1f;

        void Awake() => IsAlive = startAlive;

        public void GrantInvincibility(float seconds)
        {
            if (seconds <= 0f)
                return;

            _invincibleUntil = Mathf.Max(_invincibleUntil, Time.time + seconds);
        }

        public void ClearInvincibility() => _invincibleUntil = -1f;

        public void Die(WormChaseDeathReason reason)
        {
            if (!IsAlive || IsInvincible)
                return;

            IsAlive = false;
            if (hideOnDeath)
                gameObject.SetActive(false);

            Died?.Invoke(this, reason);
        }

        public void Revive()
        {
            IsAlive = true;
            if (hideOnDeath)
                gameObject.SetActive(true);
        }

        public void ReviveWithInvincibility(float invincibilitySeconds)
        {
            Revive();
            GrantInvincibility(invincibilitySeconds);
        }

        public void SetHideOnDeath(bool hide) => hideOnDeath = hide;
    }
}
