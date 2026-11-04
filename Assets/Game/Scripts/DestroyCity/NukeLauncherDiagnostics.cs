using Game.Core.Config;
using Game.DestroyCity.Config;
using Game.Save;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 核弹发射器诊断日志。控制台过滤关键字：<c>NukeLauncherDiag</c>
    /// </summary>
    public static class NukeLauncherDiagnostics
    {
        public const string Tag = "[NukeLauncherDiag]";

        /// <summary>设为 false 可关闭全部诊断输出。</summary>
        public static bool Enabled = true;

        public static void Log(string message, Object context = null)
        {
            if (!Enabled)
                return;

            if (context != null)
                Debug.Log($"{Tag} {message}", context);
            else
                Debug.Log($"{Tag} {message}");
        }

        public static void LogWarning(string message, Object context = null)
        {
            if (!Enabled)
                return;

            if (context != null)
                Debug.LogWarning($"{Tag} {message}", context);
            else
                Debug.LogWarning($"{Tag} {message}");
        }

        public static string DescribeObject(Object obj) =>
            obj != null ? $"{obj.name} ({obj.GetType().Name})" : "null";

        public static string DescribeTransform(Transform tr) =>
            tr != null ? $"{tr.name} pos={tr.position}" : "null";

        public static string DescribeNukeDef(NukeDef def)
        {
            if (def == null)
                return "def=null";

            return $"Lv{def.level} id={def.nukeId} radius={def.explosionRadius:F1} " +
                   $"launcher={DescribeObject(def.launcherPrefab)} " +
                   $"explosionVfx={DescribeObject(def.explosionVfxPrefab)}";
        }

        public static void LogBootstrapContext()
        {
            var data = SaveService.Data?.destroyCity;
            var db = ConfigRegistry.DestroyCityNukes;
            db?.EnsureReady();
            Log(
                $"Bootstrap | saveData={(data != null ? "ok" : "null")} " +
                $"nukeLv={NukeUpgradeService.GetNukeLevel()} nukeXp={NukeUpgradeService.GetNukeXp():F0} " +
                $"isLoaded={NukeUpgradeService.IsLoaded()} " +
                $"nukeDb={(db != null ? $"ok(entries={db.ValidEntryCount}, maxLv={db.MaxLevel})" : "null")} " +
                $"currentDef={DescribeNukeDef(NukeUpgradeService.GetCurrentNukeDef())}");
        }

        public static void DumpLauncher(NukeLauncherController launcher)
        {
            if (launcher == null)
            {
                LogWarning("DumpLauncher: launcher is null");
                return;
            }

            LogBootstrapContext();
            Log(
                $"Dump | go={launcher.name} active={launcher.isActiveAndEnabled} " +
                $"canLaunch={launcher.CanLaunch()} isLaunching={launcher.IsLaunching}",
                launcher);
            Log(
                $"Dump | launchButton={DescribeObject(launcher.GetComponentInChildren<NukeLaunchButton>(true))} " +
                $"fuelDeposit={DescribeObject(launcher.GetComponentInChildren<NukeFuelDepositZone>(true))} " +
                $"cityTarget={DescribeTransform(launcher.CityTarget)}",
                launcher);

            var bars = launcher.GetComponentsInChildren<NukeLauncherXpBar>(true);
            Log($"Dump | xpBarCount={bars.Length}", launcher);
            for (var i = 0; i < bars.Length; i++)
            {
                var bar = bars[i];
                if (bar == null)
                    continue;

                Log(
                    $"Dump | xpBar[{i}] go={bar.name} wired={bar.IsWired} score={bar.WiringScore} " +
                    $"path={GetHierarchyPath(bar.transform)}",
                    bar);
            }
        }

        static string GetHierarchyPath(Transform tr)
        {
            if (tr == null)
                return "null";

            var path = tr.name;
            var parent = tr.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
