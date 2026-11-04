using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.DestroyCity.Config
{
    [CreateAssetMenu(menuName = "Game/DestroyCity/NukeDatabase", fileName = "NukeDatabase")]
    public sealed class NukeDatabase : ScriptableObject
    {
        public List<NukeDef> nukes = new List<NukeDef>();

        Dictionary<string, NukeDef> _byId;
        List<NukeDef> _sortedByLevel;

        public int ValidEntryCount
        {
            get
            {
                EnsurePopulated();
                var count = 0;
                for (var i = 0; i < nukes.Count; i++)
                {
                    if (nukes[i] != null)
                        count++;
                }

                return count;
            }
        }

        /// <summary>启动时确保列表可用，并在条目缺失时从 Resources 回退加载。</summary>
        public void EnsureReady()
        {
            EnsurePopulated();
            InvalidateCache();
            EnsureSorted();
        }

        public bool TryGet(string id, out NukeDef def)
        {
            EnsureSorted();
            return _byId.TryGetValue(id ?? string.Empty, out def);
        }

        public bool TryGetByLevel(int level, out NukeDef def)
        {
            EnsureSorted();
            for (var i = 0; i < _sortedByLevel.Count; i++)
            {
                var n = _sortedByLevel[i];
                if (n != null && n.level == level)
                {
                    def = n;
                    return true;
                }
            }

            def = null;
            return false;
        }

        public NukeDef GetMaxLevelDef()
        {
            EnsureSorted();
            return _sortedByLevel.Count > 0 ? _sortedByLevel[_sortedByLevel.Count - 1] : null;
        }

        public int MaxLevel
        {
            get
            {
                var max = GetMaxLevelDef();
                return max != null ? max.level : 1;
            }
        }

        void OnEnable() => InvalidateCache();

        void InvalidateCache()
        {
            _byId = null;
            _sortedByLevel = null;
        }

        void EnsurePopulated()
        {
            if (nukes == null)
                nukes = new List<NukeDef>();

            if (CountValidEntries(nukes) > 0)
                return;

            InvalidateCache();
            nukes.Clear();

            var loaded = Resources.LoadAll<NukeDef>(DestroyCityResourcePaths.NukesFolder);
            for (var i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null)
                    nukes.Add(loaded[i]);
            }

            if (CountValidEntries(nukes) > 0)
            {
                nukes.Sort((a, b) => a.level.CompareTo(b.level));
                Debug.LogWarning(
                    $"[NukeDatabase] 运行时 nukes 列表为空，已通过 LoadAll 回退加载 {CountValidEntries(nukes)} 条。",
                    this);
                return;
            }

            for (var lv = 1; lv <= 64; lv++)
            {
                var def = Resources.Load<NukeDef>($"{DestroyCityResourcePaths.NukesFolder}/Nuke_Lv{lv}");
                if (def == null)
                    break;

                nukes.Add(def);
            }

            if (CountValidEntries(nukes) > 0)
            {
                Debug.LogWarning(
                    $"[NukeDatabase] 运行时 nukes 列表为空，已从 Resources/{DestroyCityResourcePaths.NukesFolder} 回退加载 {CountValidEntries(nukes)} 条。",
                    this);
            }
        }

        static int CountValidEntries(List<NukeDef> list)
        {
            if (list == null)
                return 0;

            var count = 0;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    count++;
            }

            return count;
        }

        void EnsureIndex()
        {
            if (_byId != null)
                return;

            _byId = new Dictionary<string, NukeDef>(StringComparer.Ordinal);
            foreach (var n in nukes)
            {
                if (n == null || string.IsNullOrWhiteSpace(n.nukeId))
                    continue;
                _byId[n.nukeId] = n;
            }
        }

        void EnsureSorted()
        {
            EnsurePopulated();
            EnsureIndex();
            if (_sortedByLevel != null)
                return;

            _sortedByLevel = new List<NukeDef>();
            foreach (var n in nukes)
            {
                if (n != null)
                    _sortedByLevel.Add(n);
            }

            _sortedByLevel.Sort((a, b) => a.level.CompareTo(b.level));
        }
    }
}
