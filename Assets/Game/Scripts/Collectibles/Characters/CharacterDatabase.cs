using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Collectibles.Characters
{
    [CreateAssetMenu(menuName = "Game/Collectibles/CharacterDatabase", fileName = "CharacterDatabase")]
    public sealed class CharacterDatabase : ScriptableObject
    {
        public List<CharacterDef> characters = new List<CharacterDef>();

        private Dictionary<string, CharacterDef> _byId;

        public bool TryGet(string id, out CharacterDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(id ?? string.Empty, out def);
        }

        public CharacterDef GetOrNull(string id)
        {
            TryGet(id, out var def);
            return def;
        }

        private void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, CharacterDef>(StringComparer.Ordinal);
            foreach (var c in characters)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.characterId)) continue;
                _byId[c.characterId] = c;
            }
        }
    }
}
