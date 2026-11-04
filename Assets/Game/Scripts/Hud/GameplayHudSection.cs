using Game.Core;
using UnityEngine;

namespace Game.Hud
{
    /// <summary>
    /// 标记 HUD 区块所属玩法。融合包体下仅当前 <see cref="GameplayModuleContext.ActiveModule"/> 对应区块显示。
    /// 挂到 TowerSection / FishingSection / 各模式专属按钮组等根节点上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayHudSection : MonoBehaviour
    {
        [SerializeField] private GameplayModule module = GameplayModule.Tower;
        [SerializeField] private bool includeWhenProductSingleMode = true;

        public GameplayModule Module => module;

        void OnEnable() => ApplyVisibility();

        public void ApplyVisibility()
        {
            var visible = ShouldBeVisible();
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        bool ShouldBeVisible()
        {
            if (!GameProductSettings.HasModule(module))
                return false;

            if (!GameplayModuleContext.IsFusionProduct())
                return includeWhenProductSingleMode;

            return GameplayModuleContext.IsModuleActive(module);
        }

        public static void RefreshAllInScene()
        {
#if UNITY_2023_1_OR_NEWER
            var sections = FindObjectsByType<GameplayHudSection>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var sections = FindObjectsOfType<GameplayHudSection>(true);
#endif
            for (var i = 0; i < sections.Length; i++)
                sections[i].ApplyVisibility();
        }
    }
}
