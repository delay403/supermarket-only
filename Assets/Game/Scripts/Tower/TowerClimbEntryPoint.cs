using UnityEngine;

namespace Game.Tower
{
    /// <summary>
    /// 玩家进入爬塔前的站位（可与 AI 的 climbApproach 共用）。未放置时 <see cref="TryGetPose"/> 会尝试查找名为 AIApproach 的物体。
    /// </summary>
    public sealed class TowerClimbEntryPoint : MonoBehaviour
    {
        public static TowerClimbEntryPoint Active { get; private set; }

        [SerializeField] private bool setActiveOnAwake = true;

        void Awake()
        {
            if (setActiveOnAwake || Active == null)
                Active = this;
        }

        void OnDestroy()
        {
            if (Active == this)
                Active = null;
        }

        public static bool TryGetPose(out Vector3 position, out Quaternion rotation)
        {
            if (Active != null)
            {
                position = Active.transform.position;
                rotation = Active.transform.rotation;
                return true;
            }

            var marker = Object.FindObjectOfType<TowerClimbEntryPoint>();
            if (marker != null)
            {
                position = marker.transform.position;
                rotation = marker.transform.rotation;
                return true;
            }

            var approach = GameObject.Find("AIApproach");
            if (approach != null)
            {
                position = approach.transform.position;
                rotation = approach.transform.rotation;
                return true;
            }

            position = default;
            rotation = Quaternion.identity;
            return false;
        }
    }
}
