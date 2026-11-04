using System;
using Game.Save;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>理智值服务。</summary>
    public sealed class AnimalHospitalSanityService
    {
        public event Action<float, float> Changed;

        float _max;
        float _current;

        public float Current => _current;
        public float Max => _max;
        public float Normalized => _max > 0f ? Mathf.Clamp01(_current / _max) : 0f;

        public AnimalHospitalSanityService(float max, float starting)
        {
            _max = Mathf.Max(1f, max);
            _current = Mathf.Clamp(starting, 0f, _max);
        }

        public static AnimalHospitalSanityService FromConfig(Config.AnimalHospitalConfig config)
        {
            var max = config != null ? config.maxSanity : 100f;
            var starting = config != null ? config.startingSanity : max;
            return new AnimalHospitalSanityService(max, starting);
        }

        /// <summary>优先用配置的上限/初始值；存档仅作兜底。</summary>
        public static AnimalHospitalSanityService FromSave(AnimalHospitalModeSaveData save, Config.AnimalHospitalConfig config)
        {
            if (config != null)
                return FromConfig(config);

            var max = save != null ? save.maxSanity : 100f;
            var current = save != null ? save.sanity : max;
            return new AnimalHospitalSanityService(max, current);
        }

        /// <summary>开局/新一天：按配置重置理智。</summary>
        public void ResetForNewDay(Config.AnimalHospitalConfig config)
        {
            var max = config != null ? Mathf.Max(1f, config.maxSanity) : 100f;
            var starting = config != null ? config.startingSanity : max;
            _max = max;
            ForceSet(Mathf.Clamp(starting, 0f, _max));
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f)
                return;
            Set(_current - amount);
        }

        public void Restore(float amount)
        {
            if (amount <= 0f)
                return;
            Set(_current + amount);
        }

        public void Persist(AnimalHospitalModeSaveData save)
        {
            if (save == null)
                return;
            save.maxSanity = _max;
            save.sanity = _current;
        }

        void Set(float value)
        {
            var next = Mathf.Clamp(value, 0f, _max);
            if (Mathf.Approximately(next, _current))
                return;
            ForceSet(next);
        }

        void ForceSet(float value)
        {
            _current = Mathf.Clamp(value, 0f, _max);
            Changed?.Invoke(_current, _max);
        }
    }
}
