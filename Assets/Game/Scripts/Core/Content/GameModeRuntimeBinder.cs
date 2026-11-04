using System.Collections.Generic;
using Game.Audio;
using Game.VFX;
using UnityEngine;

namespace Game.Core.Content
{
    /// <summary>
    /// 挂在玩法场景上，在场景加载后将本模式的 SFX / VFX 合并进全局运行时服务。
    /// MainHome 的 <see cref="GameBootstrapper"/> 只需保留通用资源，模式专属资源放各自场景。
    /// </summary>
    public sealed class GameModeRuntimeBinder : MonoBehaviour
    {
        [SerializeField] private List<AudioManager.SfxClipEntry> modeSfxLibrary = new List<AudioManager.SfxClipEntry>();
        [SerializeField] private List<GameplayVfxManager.VfxPrefabBinding> modeVfxBindings = new List<GameplayVfxManager.VfxPrefabBinding>();

        public void ApplyToRuntimeServices()
        {
            if (modeSfxLibrary != null && modeSfxLibrary.Count > 0)
            {
                var audio = AudioManager.Instance;
                if (audio != null)
                    audio.MergeSfxLibrary(modeSfxLibrary);
            }

            if (modeVfxBindings != null && modeVfxBindings.Count > 0)
                GameplayVfxManager.MergeRuntimeBindings(modeVfxBindings);
        }
    }
}
