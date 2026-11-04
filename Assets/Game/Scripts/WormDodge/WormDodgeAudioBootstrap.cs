using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// 躲避虫子音效：在 Bootstrap 未绑定时，从 Resources 或场景内 WormChainMotorAudio 回退注册。
    /// </summary>
    public static class WormDodgeAudioBootstrap
    {
        const string MoveLoopResourcePath = "EscapeWorm/WormMoveLoop";
        const string InsideWallLoopResourcePath = "EscapeWorm/WormInsideWallLoop";

        static bool _registered;

        public static void EnsureRegistered()
        {
            if (!GameProductSettings.IsWormDodgeContentEnabled)
                return;

            if (_registered)
                return;

            var am = AudioManager.EnsureCreated();
            if (am == null)
                return;

            RegisterIfMissing(am, SfxId.WormMoveLoop, MoveLoopResourcePath);
            RegisterIfMissing(am, SfxId.WormInsideWallLoop, InsideWallLoopResourcePath);
            _registered = true;
        }

        static void RegisterIfMissing(AudioManager am, SfxId id, string resourcePath)
        {
            if (am.TryGetSfxClip(id, out _, out _))
                return;

            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip != null)
                am.EnsureSfx(id, clip);
        }
    }
}
