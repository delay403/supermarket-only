using System;
using Game.Core.Config;
using Game.DestroyCity.Config;
using Game.Save;
using UnityEngine;

namespace Game.DestroyCity
{
    public static class NukeUpgradeService
    {
        public static event Action<int> NukeUpgraded;
        public static event Action<int> NukeLevelIncreased;
        public static event Action<float> NukeXpChanged;
        public static event Action<float> FuelXpInfused;

        static DestroyCityModeSaveData Data => SaveService.Data?.destroyCity;

        public static int GetNukeLevel() => Mathf.Max(1, Data?.nukeLevel ?? 1);

        public static float GetNukeXp() => Mathf.Max(0f, Data?.nukeXp ?? 0f);

        public static NukeDef GetCurrentNukeDef()
        {
            var level = GetNukeLevel();
            var db = ConfigRegistry.DestroyCityNukes;
            db?.EnsureReady();
            if (db != null && db.TryGetByLevel(level, out var def))
                return def;

            var fromResources = LoadNukeDefFromResources(level);
            if (fromResources != null)
            {
                NukeLauncherDiagnostics.Log(
                    $"GetCurrentNukeDef: 数据库无 Lv{level}，使用 Resources 条目 | " +
                    $"{NukeLauncherDiagnostics.DescribeNukeDef(fromResources)}");
                return fromResources;
            }

            var fallback = CreateFallbackDef(level);
            NukeLauncherDiagnostics.Log(
                $"GetCurrentNukeDef: 数据库无 Lv{level}，使用 fallback | " +
                $"{NukeLauncherDiagnostics.DescribeNukeDef(fallback)}");
            return fallback;
        }

        public static NukeDef GetNextNukeDef()
        {
            var nextLevel = GetNukeLevel() + 1;
            var db = ConfigRegistry.DestroyCityNukes;
            if (db != null && db.TryGetByLevel(nextLevel, out var def))
                return def;
            return null;
        }

        public static bool IsMaxLevel()
        {
            var db = ConfigRegistry.DestroyCityNukes;
            return db == null || GetNukeLevel() >= db.MaxLevel;
        }

        public static float GetUpgradeXpRequired()
        {
            var next = GetNextNukeDef();
            if (next == null)
                return 0f;
            return Mathf.Max(0f, next.upgradeXpRequired);
        }

        /// <summary>是否可发射（默认可用 Lv1 / 0 经验的基础导弹）。</summary>
        public static bool IsLoaded() => Data != null && GetCurrentNukeDef() != null;

        public struct LaunchSnapshot
        {
            public bool success;
            public int level;
            public float xp;
            public NukeDef def;
        }

        /// <summary>发射消耗：结算当前等级后重置为 Lv1 / 0 经验（Lv1 且 0 经验亦可发射）。</summary>
        public static LaunchSnapshot ConsumeForLaunch()
        {
            var snapshot = new LaunchSnapshot
            {
                success = false,
                level = GetNukeLevel(),
                xp = GetNukeXp(),
                def = GetCurrentNukeDef(),
            };

            if (Data == null || snapshot.def == null)
            {
                NukeLauncherDiagnostics.LogWarning(
                    $"ConsumeForLaunch 失败 | data={(Data != null ? "ok" : "null")} " +
                    $"def={(snapshot.def != null ? snapshot.def.nukeId : "null")}");
                return snapshot;
            }

            NukeLauncherDiagnostics.Log(
                $"ConsumeForLaunch 成功 | Lv{snapshot.level} xp={snapshot.xp:F0} " +
                $"def={NukeLauncherDiagnostics.DescribeNukeDef(snapshot.def)}");

            ResetToBaselineMissile(save: true);

            snapshot.success = true;
            return snapshot;
        }

        /// <summary>爆炸或发射后恢复为默认可发射的 Lv1 / 0 经验导弹。</summary>
        public static void ResetToBaselineMissile(bool save = false)
        {
            if (Data == null)
                return;

            NukeLauncherDiagnostics.Log($"ResetToBaselineMissile | save={save}");

            Data.nukeLevel = 1;
            Data.nukeXp = 0f;
            if (save)
                SaveService.Save();
            NukeUpgraded?.Invoke(Data.nukeLevel);
            NukeXpChanged?.Invoke(Data.nukeXp);
        }

        /// <summary>灌注燃料：扣除燃料代表的经验，累加到核弹并自动升级。</summary>
        public static bool TryInfuseFuelXp(float fuelTotalXp)
        {
            if (Data == null || fuelTotalXp <= 0f)
            {
                NukeLauncherDiagnostics.LogWarning(
                    $"TryInfuseFuelXp 失败 | data={(Data != null ? "ok" : "null")} fuelXp={fuelTotalXp:F0}");
                return false;
            }

            var beforeLevel = Data.nukeLevel;
            var beforeXp = Data.nukeXp;

            Data.nukeXp += fuelTotalXp;
            FuelXpInfused?.Invoke(fuelTotalXp);
            while (!IsMaxLevel())
            {
                var need = GetUpgradeXpRequired();
                if (need <= 0f || Data.nukeXp + 1e-4f < need)
                    break;

                Data.nukeXp -= need;
                Data.nukeLevel++;
                NukeUpgraded?.Invoke(Data.nukeLevel);
                NukeLevelIncreased?.Invoke(Data.nukeLevel);
            }

            SaveService.Save();
            NukeXpChanged?.Invoke(Data.nukeXp);
            NukeLauncherDiagnostics.Log(
                $"TryInfuseFuelXp 成功 | +{fuelTotalXp:F0}xp " +
                $"Lv{beforeLevel}/{beforeXp:F0} -> Lv{Data.nukeLevel}/{Data.nukeXp:F0} " +
                $"maxLevel={IsMaxLevel()}");
            return true;
        }

        static NukeDef LoadNukeDefFromResources(int level)
        {
            level = Mathf.Max(1, level);
            var def = Resources.Load<NukeDef>($"{DestroyCityResourcePaths.NukesFolder}/Nuke_Lv{level}");
            if (def != null)
                return def;

            var all = Resources.LoadAll<NukeDef>(DestroyCityResourcePaths.NukesFolder);
            for (var i = 0; i < all.Length; i++)
            {
                var candidate = all[i];
                if (candidate != null && candidate.level == level)
                    return candidate;
            }

            return null;
        }

        static NukeDef CreateFallbackDef(int level)
        {
            level = Mathf.Max(1, level);
            var db = ConfigRegistry.DestroyCityNukes;
            db?.EnsureReady();
            NukeDef visualTemplate = LoadNukeDefFromResources(level);
            if (db != null)
            {
                if (visualTemplate == null && !db.TryGetByLevel(level, out visualTemplate))
                {
                    var capped = Mathf.Clamp(level, 1, db.MaxLevel);
                    db.TryGetByLevel(capped, out visualTemplate);
                }

                visualTemplate ??= db.GetMaxLevelDef();
                if (visualTemplate == null)
                    db.TryGetByLevel(1, out visualTemplate);
            }

            visualTemplate ??= LoadNukeDefFromResources(1);

            var bal = ConfigRegistry.Balance;
            var def = ScriptableObject.CreateInstance<NukeDef>();
            def.level = level;
            def.nukeId = visualTemplate != null ? visualTemplate.nukeId : $"nuke_fallback_{level}";
            def.displayName = visualTemplate != null ? visualTemplate.displayName : $"Nuke Lv{level}";
            def.upgradeXpRequired = visualTemplate != null
                ? visualTemplate.upgradeXpRequired
                : bal != null
                    ? bal.destroyCityFallbackNukeUpgradeXp * level
                    : 100f * level;
            def.explosionRadius = visualTemplate != null
                ? visualTemplate.explosionRadius
                : bal != null
                    ? bal.destroyCityFallbackExplosionRadius + level * 10f
                    : 50f + level * 10f;
            def.coinRewardMultiplier = visualTemplate != null
                ? visualTemplate.coinRewardMultiplier
                : 1f + (level - 1) * 0.2f;
            def.launcherPrefab = visualTemplate != null ? visualTemplate.launcherPrefab : null;
            def.explosionVfxPrefab = visualTemplate != null ? visualTemplate.explosionVfxPrefab : null;
            def.icon = visualTemplate != null ? visualTemplate.icon : null;
            return def;
        }
    }
}
