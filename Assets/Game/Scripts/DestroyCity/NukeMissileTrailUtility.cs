using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>控制导弹预制体下名为 Trail 的拖尾子物体。</summary>
    public static class NukeMissileTrailUtility
    {
        const string TrailChildName = "Trail";

        public static void SetTrailActive(GameObject missileRoot, bool active)
        {
            if (missileRoot == null)
                return;

            var trail = FindTrailTransform(missileRoot.transform);
            if (trail == null)
                return;

            trail.gameObject.SetActive(active);
            ResetTrailRenderers(trail);
        }

        static Transform FindTrailTransform(Transform root)
        {
            if (root.name == TrailChildName)
                return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindTrailTransform(root.GetChild(i));
                if (found != null)
                    return found;
            }

            return null;
        }

        static void ResetTrailRenderers(Transform trail)
        {
            var trails = trail.GetComponentsInChildren<TrailRenderer>(true);
            for (var i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null)
                    continue;
                trails[i].Clear();
                trails[i].emitting = trails[i].gameObject.activeInHierarchy;
            }

            var particles = trail.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null)
                    continue;
                particles[i].Clear(true);
                if (particles[i].gameObject.activeInHierarchy)
                    particles[i].Play(true);
            }
        }
    }
}
