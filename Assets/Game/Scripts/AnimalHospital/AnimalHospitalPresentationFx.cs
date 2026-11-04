using Game.Audio;
using UnityEngine;

namespace Game.AnimalHospital
{
    public static class AnimalHospitalPresentationFx
    {
        public static void PlayReceptionBell(Vector3 worldPosition, AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
                return;

            var volume = Mathf.Clamp01(volumeScale);
            if (AudioManager.Instance != null)
                volume *= AudioManager.Instance.UserSfxVolume01;

            AudioSource.PlayClipAtPoint(clip, worldPosition, volume);
        }
    }
}
