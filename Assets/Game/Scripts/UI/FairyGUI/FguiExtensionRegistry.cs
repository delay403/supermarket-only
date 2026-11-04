using Game.Core;
using Game.Maps;
using UnityEngine;

namespace Game.UI.FairyGUI
{
    /// <summary>
    /// 必须在加载任何 FGUI 包实例化 UI 之前注册 <c>UIObjectFactory.SetPackageItemExtension</c>，
    /// 否则根组件会是 <see cref="FairyGUI.GComponent"/> 基类而非 <c>UIGameWnd</c> 等派生类型，子组件也无法解析为 <c>UISlider</c>。
    /// </summary>
    internal static class FguiExtensionRegistry
    {
        private static bool s_registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAllBinders()
        {
            if (s_registered) return;
            s_registered = true;

            var modules = GameProductSettings.ResolveEnabledModules();
            var settings = GameProductSettings.ResolveActive();
            var nav = SceneNavigationSettings.ResolveActive();

            if (GameProductBuildReferenceRulesRuntime.NeedsFguiGameHud(modules))
                Game.GameBinder.BindAll();

            if (nav != null
                && nav.useMainHomeScene
                && settings != null
                && settings.BuildAvailableModes().Count > 1)
                Home.HomeBinder.BindAll();

            Loading.LoadingBinder.BindAll();

            if (!GameProductBuildReferenceRulesRuntime.ShouldStripOptionalFguiUi(modules))
            {
                Setting.SettingBinder.BindAll();
            }
        }
    }

    static class GameProductBuildReferenceRulesRuntime
    {
        public static bool NeedsFguiGameHud(GameplayModule modules) =>
            (modules & (GameplayModule.Tower
                         | GameplayModule.Fishing
                         | GameplayModule.DestroyCity
                         | GameplayModule.WormDodge)) != 0;

        public static bool ShouldStripOptionalFguiUi(GameplayModule modules) =>
            !NeedsFguiGameHud(modules)
            && (modules & GameplayModule.SupermarketOrganize) != 0;
    }
}
