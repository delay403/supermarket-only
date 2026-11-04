using UnityEngine;

namespace Game.EscapeSnake
{
    /// <summary>
    /// 死亡区域：目标进入本物体 Trigger 后死亡。请挂在安全区子物体上，勿与安全区 BoxCollider 混用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDeathZone : MonoBehaviour
    {
        [SerializeField] private WormChaseDeathReason deathReason = WormChaseDeathReason.DeathZone;

        void Awake()
        {
            if (GetComponent<WormSafeZone>() != null)
                Debug.LogWarning($"[WormDeathZone] {name} 与安全区组件同处一物，请把死亡区放在子物体上。", this);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!TryGetTarget(other, out var target))
                return;

            if (target.IsAlive)
                target.Die(deathReason);
        }

        static bool TryGetTarget(Collider other, out WormChaseTarget target)
        {
            target = other.GetComponent<WormChaseTarget>();
            if (target != null)
                return true;

            target = other.GetComponentInParent<WormChaseTarget>();
            return target != null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }
#endif
    }
}
