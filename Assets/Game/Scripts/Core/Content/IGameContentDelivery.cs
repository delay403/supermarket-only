using System;
using System.Threading.Tasks;
using Game.Core;

namespace Game.Core.Content
{
    /// <summary>
    /// 远程内容分包投递（CDN / AssetBundle）。可由外部实现并注入 <see cref="GameContentService"/>。
    /// </summary>
    public interface IGameContentDelivery
    {
        bool UseRemotePackages { get; }

        bool IsPackageReady(GameplayModule module);

        Task EnsurePackageReadyAsync(GameplayModule module, IProgress<float> progress = null);
    }
}
