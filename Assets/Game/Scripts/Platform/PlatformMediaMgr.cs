using System;
using UnityEngine;

namespace Game.Platform
{
    /// <summary>
    /// 平台侧媒体设置（音量、振动）与游戏 <see cref="Game.Audio.AudioManager"/> 的双向同步。
    /// </summary>
    public static class PlatformMediaMgr
    {
        public const string SfxVolumeKey = "PlatformMedia_SfxVolume01";
        public const string BgmVolumeKey = "PlatformMedia_BgmVolume01";
        public const string VibrationKey = "PlatformMedia_VibrationEnabled";

        public static event Action<float> UserSfxVolume01Changed;
        public static event Action<float> UserBgmVolume01Changed;

        public static float UserSfxVolume01
        {
            get => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            set => SetVolume(value, SfxVolumeKey, UserSfxVolume01Changed);
        }

        public static float UserBgmVolume01
        {
            get => PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
            set => SetVolume(value, BgmVolumeKey, UserBgmVolume01Changed);
        }

        public static bool VibrationEnabled
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        static void SetVolume(float linear01, string key, Action<float> changed)
        {
            linear01 = Mathf.Clamp01(linear01);
            var previous = PlayerPrefs.GetFloat(key, 1f);
            if (Mathf.Approximately(previous, linear01))
                return;

            PlayerPrefs.SetFloat(key, linear01);
            PlayerPrefs.Save();
            changed?.Invoke(linear01);
        }
    }
}
