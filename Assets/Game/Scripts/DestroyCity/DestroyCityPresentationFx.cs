using Game.Audio;
using Game.VFX;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 摧毁城市玩法视听反馈；粒子与音效预制体在 <see cref="Game.Core.GameBootstrapper"/> 中绑定。
    /// </summary>
    public static class DestroyCityPresentationFx
    {
        public static void PlayFuelUpgrade(Vector3 worldPosition)
        {
            PlayWorldVfx(VfxKind.DestroyCityFuelUpgrade, worldPosition, 2.5f);
            AudioManager.Instance?.PlaySfx3D(SfxId.DestroyCityFuelUpgrade, worldPosition);
        }

        public static void PlayNukeUpgrade(Vector3 worldPosition)
        {
            PlayWorldVfx(VfxKind.DestroyCityNukeUpgrade, worldPosition, 2.5f);
            AudioManager.Instance?.PlaySfx3D(SfxId.DestroyCityNukeUpgrade, worldPosition);
        }

        public static void PlayMissileLaunch(Vector3 worldPosition)
        {
            PlayWorldVfx(VfxKind.DestroyCityMissileLaunch, worldPosition, 2f);
            AudioManager.Instance?.PlaySfx3D(SfxId.DestroyCityMissileLaunch, worldPosition);
        }

        public static void StartMissileAlarm()
        {
            AudioManager.Instance?.PlayLoopingSfx(SfxId.DestroyCityMissileAlarm);
        }

        public static void StopMissileAlarm()
        {
            AudioManager.Instance?.StopLoopingSfx();
        }

        public static void PlayMissileExplosion(Vector3 worldPosition)
        {
            AudioManager.Instance?.PlaySfx3D(SfxId.DestroyCityMissileExplosion, worldPosition);
        }

        public static void PlayFuelProduceButton(Vector3 worldPosition)
        {
            AudioManager.Instance?.PlaySfx3D(SfxId.DestroyCityFuelProduceButton, worldPosition);
        }

        public static void PlayFuelXpGain(Vector3 worldPosition)
        {
            AudioManager.Instance?.PlaySfx3D(SfxId.DestroyCityFuelXpGain, worldPosition);
        }

        public static void PlayLaunchButton(Vector3 worldPosition)
        {
            AudioManager.Instance?.PlaySfx3D(SfxId.DestroyCityLaunchButton, worldPosition);
        }

        public static void StartMissileFlightLoop()
        {
            AudioManager.Instance?.PlayLoopingSfx(SfxId.DestroyCityMissileFlight);
        }

        public static void StopMissileFlightLoop()
        {
            AudioManager.Instance?.StopLoopingSfx();
        }

        static void PlayWorldVfx(VfxKind kind, Vector3 worldPosition, float releaseAfterSeconds)
        {
            var mgr = GameplayVfxManager.Instance;
            if (mgr == null || !mgr.HasKind(kind))
                return;

            mgr.BorrowWorld(kind, worldPosition, Quaternion.identity, Vector3.one, releaseAfterSeconds);
        }
    }
}
