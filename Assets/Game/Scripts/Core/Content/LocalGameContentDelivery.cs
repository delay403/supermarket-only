using System;
using System.Threading.Tasks;
using Game.Core;

namespace Game.Core.Content
{
    /// <summary>
    /// 单包体模式：模式配置由 Build 场景 / Bootstrapper 直接引用链打入包体，无需远程下载。
    /// </summary>
    public sealed class LocalGameContentDelivery : IGameContentDelivery
    {
        public static readonly LocalGameContentDelivery Instance = new LocalGameContentDelivery();

        public bool UseRemotePackages => false;

        public bool IsPackageReady(GameplayModule module) => true;

        public Task EnsurePackageReadyAsync(GameplayModule module, IProgress<float> progress = null) =>
            Task.CompletedTask;
    }
}
