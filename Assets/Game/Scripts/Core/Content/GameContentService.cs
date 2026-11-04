using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Core.Content
{
    /// <summary>
    /// 运行时内容注册表：优先使用场景/Bootstrapper 注入的直接引用，替代模式级 Resources.Load。
    /// </summary>
    public static class GameContentService
    {
        static readonly Dictionary<Type, UnityEngine.Object> s_assets = new Dictionary<Type, UnityEngine.Object>();
        static IGameContentDelivery s_delivery;

        public static IGameContentDelivery Delivery
        {
            get => s_delivery ?? LocalGameContentDelivery.Instance;
            set => s_delivery = value;
        }

        public static void InitializeFromSettings()
        {
            Delivery = LocalGameContentDelivery.Instance;
        }

        public static void Register<T>(T asset) where T : UnityEngine.Object
        {
            if (asset == null)
                return;

            s_assets[typeof(T)] = asset;
        }

        public static void RegisterBinderAssets(GameModeConfigBinder binder)
        {
            if (binder == null)
                return;

            binder.PopulateRegistrations(s_assets);
        }

        public static T Resolve<T>() where T : UnityEngine.Object
        {
            if (s_assets.TryGetValue(typeof(T), out var asset) && asset is T typed)
                return typed;

            return null;
        }

        public static T ResolveOrDefault<T>(T fallback) where T : UnityEngine.Object =>
            Resolve<T>() ?? fallback;

        public static void MarkPackageReady(GameplayModule module)
        {
            // 本地单包体模式：模块内容随包体加载，无需额外标记。
        }

        public static bool IsModulePackageReady(GameplayModule module) =>
            Delivery.IsPackageReady(module);

        public static Task EnsureModuleReadyAsync(GameplayModule module, IProgress<float> progress = null) =>
            Delivery.EnsurePackageReadyAsync(module, progress);

        public static void ClearForTests()
        {
            s_assets.Clear();
            s_delivery = null;
        }
    }
}
