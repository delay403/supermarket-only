using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.StrengthTraining
{
    [CreateAssetMenu(
        menuName = "Game/Features/Strength Training Ground Database",
        fileName = "StrengthTrainingGroundDatabase")]
    public sealed class StrengthTrainingGroundDatabase : ScriptableObject
    {
        public List<StrengthTrainingGroundDef> grounds = new List<StrengthTrainingGroundDef>();

        Dictionary<string, StrengthTrainingGroundDef> _byId;

        public bool TryGet(string id, out StrengthTrainingGroundDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(NormalizeId(id), out def);
        }

        public StrengthTrainingGroundDef GetOrNull(string id)
        {
            TryGet(id, out var def);
            return def;
        }

        void EnsureIndex()
        {
            if (_byId != null)
                return;

            _byId = new Dictionary<string, StrengthTrainingGroundDef>(StringComparer.Ordinal);
            for (var i = 0; i < grounds.Count; i++)
            {
                var g = grounds[i];
                if (g == null || string.IsNullOrWhiteSpace(g.groundId))
                    continue;
                _byId[NormalizeId(g.groundId)] = g;
            }
        }

        static string NormalizeId(string id) => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }
}
