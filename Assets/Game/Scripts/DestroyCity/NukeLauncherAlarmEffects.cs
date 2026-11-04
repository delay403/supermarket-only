using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>发射警报阶段场景特效（默认关闭，仅警报时播放）。</summary>
    [DisallowMultipleComponent]
    public sealed class NukeLauncherAlarmEffects : MonoBehaviour
    {
        [SerializeField] private GameObject[] effectRoots;

        void Awake()
        {
            if (effectRoots == null || effectRoots.Length == 0)
                effectRoots = CollectDefaultEffects();
            SetActiveAll(false);
        }

        public void SetAlarmActive(bool active)
        {
            if (effectRoots == null)
                return;

            for (var i = 0; i < effectRoots.Length; i++)
            {
                if (effectRoots[i] == null)
                    continue;
                effectRoots[i].SetActive(active);
                if (active)
                    RestartParticles(effectRoots[i]);
            }
        }

        static void RestartParticles(GameObject root)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null)
                    continue;
                systems[i].Clear(true);
                systems[i].Play(true);
            }
        }

        GameObject[] CollectDefaultEffects()
        {
            var list = new System.Collections.Generic.List<GameObject>();
            CollectByName(transform, "deng", list);
            return list.ToArray();
        }

        static void CollectByName(Transform root, string namePrefix, System.Collections.Generic.List<GameObject> list)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.StartsWith(namePrefix))
                    list.Add(child.gameObject);
                CollectByName(child, namePrefix, list);
            }
        }

        void SetActiveAll(bool active)
        {
            if (effectRoots == null)
                return;

            for (var i = 0; i < effectRoots.Length; i++)
            {
                if (effectRoots[i] != null)
                    effectRoots[i].SetActive(active);
            }
        }
    }
}
