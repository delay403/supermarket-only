using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Collectibles.Pets
{
    [CreateAssetMenu(menuName = "Game/Collectibles/PetDatabase", fileName = "PetDatabase")]
    public sealed class PetDatabase : ScriptableObject
    {
        public List<PetDef> pets = new List<PetDef>();

        private Dictionary<string, PetDef> _byId;

        public IEnumerable<PetDef> All => pets;

        public bool TryGet(string id, out PetDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(id ?? string.Empty, out def);
        }

        public PetDef GetOrNull(string id)
        {
            TryGet(id, out var def);
            return def;
        }

        private void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, PetDef>(StringComparer.Ordinal);
            foreach (var p in pets)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.petId)) continue;
                _byId[p.petId] = p;
            }
        }

        /// <summary>
        /// 按 <see cref="PetDef.quality"/>（绿最低）、再 <see cref="PetDef.level"/>、再 <see cref="PetDef.petId"/> 取最低等一只，用于新档赠送。
        /// </summary>
        public string GetLowestTierPetIdOrDefault(string fallbackPetId = "pet_1")
        {
            EnsureIndex();
            if (pets == null || pets.Count == 0)
                return string.IsNullOrWhiteSpace(fallbackPetId) ? string.Empty : fallbackPetId;

            PetDef best = null;
            for (var i = 0; i < pets.Count; i++)
            {
                var p = pets[i];
                if (p == null || string.IsNullOrWhiteSpace(p.petId)) continue;
                if (best == null)
                {
                    best = p;
                    continue;
                }

                var q = ((int)p.quality).CompareTo((int)best.quality);
                if (q < 0)
                {
                    best = p;
                    continue;
                }

                if (q > 0) continue;

                if (p.level != best.level)
                {
                    if (p.level < best.level) best = p;
                    continue;
                }

                if (string.CompareOrdinal(p.petId, best.petId) < 0)
                    best = p;
            }

            return best != null ? best.petId : fallbackPetId;
        }
    }
}

