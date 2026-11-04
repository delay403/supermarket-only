using UnityEngine;

namespace Game.WormDodge.Pickups
{
    /// <summary>
    /// 在开局时于挂载点下随机生成一个道具预制体；新一局会销毁并重新随机。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeRandomPickupSpawner : MonoBehaviour
    {
        [SerializeField] private Transform spawnAnchor;
        [SerializeField] private GameObject[] pickupPrefabs;

        GameObject _spawnedInstance;

        void Awake()
        {
            if (spawnAnchor == null)
                spawnAnchor = transform;
        }

        void OnEnable() => WormDodgeDirector.RunReset += OnRunReset;

        void OnDisable()
        {
            WormDodgeDirector.RunReset -= OnRunReset;
            ClearSpawned();
        }

        void OnRunReset() => RespawnRandomPickup();

        void RespawnRandomPickup()
        {
            ClearSpawned();

            if (pickupPrefabs == null || pickupPrefabs.Length == 0 || spawnAnchor == null)
                return;

            GameObject prefab = null;
            for (var attempt = 0; attempt < pickupPrefabs.Length && prefab == null; attempt++)
            {
                var candidate = pickupPrefabs[Random.Range(0, pickupPrefabs.Length)];
                if (candidate != null)
                    prefab = candidate;
            }

            if (prefab == null)
                return;

            _spawnedInstance = Instantiate(prefab, spawnAnchor);
            _spawnedInstance.transform.localPosition = Vector3.zero;
            _spawnedInstance.transform.localRotation = Quaternion.identity;
            _spawnedInstance.transform.localScale = Vector3.one;
        }

        void ClearSpawned()
        {
            if (_spawnedInstance == null)
                return;

            if (Application.isPlaying)
                Destroy(_spawnedInstance);
            else
                DestroyImmediate(_spawnedInstance);

            _spawnedInstance = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (spawnAnchor == null)
                spawnAnchor = transform;
        }
#endif
    }
}
