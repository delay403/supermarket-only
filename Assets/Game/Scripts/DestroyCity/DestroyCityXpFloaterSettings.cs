using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 场景级飘字预制体配置。将自制预制体拖到 <see cref="prefab"/> 即可。
    /// 预制体根节点需挂 <see cref="DestroyCityXpFloatView"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestroyCityXpFloaterSettings : MonoBehaviour
    {
        [SerializeField] private DestroyCityXpFloatView prefab;

        public DestroyCityXpFloatView Prefab => prefab;

        void Awake() => ApplyToFloater();

        public void ApplyToFloater()
        {
            if (prefab != null)
                DestroyCityXpFloater.RegisterPrefab(prefab);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (prefab != null && prefab.GetComponent<DestroyCityXpFloatView>() == null)
                Debug.LogWarning("[DestroyCityXpFloaterSettings] 预制体根节点需挂 DestroyCityXpFloatView。", this);
        }
#endif
    }
}
