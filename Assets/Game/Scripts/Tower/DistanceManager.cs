using UnityEngine;

namespace Game.Tower
{
    /// <summary>
    /// 从 <see cref="TowerManager.towerRoot"/> 起算水平面滑翔距离（米，XZ）。
    /// </summary>
    public static class DistanceManager
    {
        public static float HorizontalGlideMetersFromTowerRoot(Vector3 worldPosition, TowerManager tower)
        {
            if (tower == null)
                return 0f;

            var origin = tower.towerRoot != null
                ? tower.towerRoot.position
                : new Vector3(0f, tower.BaseY, 0f);

            var dx = worldPosition.x - origin.x;
            var dz = worldPosition.z - origin.z;
            return new Vector2(dx, dz).magnitude;
        }
    }
}
