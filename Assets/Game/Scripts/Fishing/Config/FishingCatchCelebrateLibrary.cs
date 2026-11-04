using System;
using System.Collections.Generic;
using Game.Audio;
using UnityEngine;

namespace Game.Fishing.Config
{
    /// <summary>
    /// 按角色配置钓获庆祝特效与音效；音效在 <see cref="Game.UI.Fishing.FishingCatchRewardToastView"/> 出现时播放。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Fishing/Catch Celebrate Library", fileName = "FishingCatchCelebrateLibrary")]
    public sealed class FishingCatchCelebrateLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("与 CharacterDatabase.characterId 一致。")]
            public string characterId;

            [Tooltip("留空则使用 GameplayVfxManager 的 FishingCatchCelebrate 或 PresentationFx 上的 Override。")]
            public GameObject celebratePrefab;

            public SfxId celebrateSfx = SfxId.FishingCatchCelebrate1;

            [Min(0.1f)]
            public float playDurationSeconds = 2f;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private Entry defaultEntry = new Entry();

        public Entry Resolve(string characterId)
        {
            if (!string.IsNullOrWhiteSpace(characterId))
            {
                var id = characterId.Trim();
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.characterId))
                        continue;
                    if (string.Equals(entry.characterId.Trim(), id, StringComparison.Ordinal))
                        return entry;
                }
            }

            return defaultEntry;
        }
    }
}
