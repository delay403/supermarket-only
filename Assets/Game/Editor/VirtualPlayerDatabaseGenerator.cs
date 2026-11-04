#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Game.Leaderboard;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 一键生成 20～25 条 <see cref="VirtualPlayerDef"/> 并写入一条 <see cref="VirtualPlayerDatabase"/>。
    /// </summary>
    public static class VirtualPlayerDatabaseGenerator
    {
        const string MenuPath = "Tools/RobloxPata/Leaderboard/Generate Virtual Players (20-25)...";

        /// <summary>常用汉字池（随机组合成 2～6 字昵称）。</summary>
        static readonly string CjkPool =
            "的一是在不了有和人这中大为上个国我以要他时来用们生到作地于出就分对成会可主发年动同工也能下过子说产种面而方后多定行学法所民得经十三之进着等部度家电力里如水化高自二理起小物现实加量都两体制机当使点从业本去把性好应开它合还因由其些然前外天政四日那社义事平形相全表间样与关各重新线内数正心反你明看原又么利比或但质气第向道命此变条只没结解问意建月公无系军很情者最立代想已通并提直题党程展五果料象员革位入常文总次品式活设及管特件长求老头基资边流路级少图山统接知较将组见计别她手角期根论运农指几九区强放决西被干做必战先回则任取据处队南给色光门即保治北造百规热领七海口东导器压志世金增争济阶油思术极交受联什认六共权收证改清己美再采转更单风切打白教速花带安场身车例真务具万每目至达走积示议声报斗完类八离华名确才科张信马节话米整空元况今集温传土许步群广石记需段研界拉林律叫且究观越织装影算低持音众书布复容须际商非验连断深难近矿千周委素技备半办青省列习响约支般史感劳便团往酸历市克何除消构府称太准精值号率族维划选标写存候毛亲快效斯院查江型眼王按格养易置派层片始却专状育厂京识适属圆包火住调满县局照参红细引听该铁价严龙飞";

        [MenuItem(MenuPath)]
        static void GenerateMenu()
        {
            var absFolder = EditorUtility.SaveFolderPanel(
                "选择生成目录（需在 Assets 内）",
                Application.dataPath + "/Game",
                "LeaderboardVirtualPlayers");

            if (string.IsNullOrEmpty(absFolder))
                return;

            var projectAssets = Application.dataPath.Replace('\\', '/');
            var normalized = absFolder.Replace('\\', '/');
            if (!normalized.StartsWith(projectAssets, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("无效路径", "请选择项目 Assets 文件夹内的目录。", "确定");
                return;
            }

            var relativeFolder = "Assets" + normalized.Substring(projectAssets.Length);

            var count = UnityEngine.Random.Range(20, 26); // 20～25（上限 exclusive）
            var defs = new List<VirtualPlayerDef>(count);

            try
            {
                AssetDatabase.StartAssetEditing();
                for (var i = 0; i < count; i++)
                {
                    var def = ScriptableObject.CreateInstance<VirtualPlayerDef>();
                    def.virtualId = Guid.NewGuid().ToString("N");
                    def.displayName = GenerateRandomDisplayName();
                    def.coins = UnityEngine.Random.Range(1000, 1_000_001); // int：上限 exclusive
                    def.glideDistanceMeters = UnityEngine.Random.Range(1000f, 30_000f);

                    var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{relativeFolder}/VirtualPlayer_{def.virtualId.Substring(0, 8)}.asset");
                    AssetDatabase.CreateAsset(def, assetPath);
                    defs.Add(def);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            var dbPath = AssetDatabase.GenerateUniqueAssetPath($"{relativeFolder}/VirtualPlayerDatabase.asset");
            var db = ScriptableObject.CreateInstance<VirtualPlayerDatabase>();
            db.players = new List<VirtualPlayerDef>(defs);
            AssetDatabase.CreateAsset(db, dbPath);
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = db;
            EditorUtility.DisplayDialog(
                "生成完成",
                $"已生成 {count} 条 VirtualPlayerDef，并写入数据库：\n{dbPath}",
                "确定");
        }

        static string GenerateRandomDisplayName()
        {
            return UnityEngine.Random.value < 0.5f ? RandomChineseName() : RandomLatinName();
        }

        static string RandomChineseName()
        {
            var len = UnityEngine.Random.Range(2, 7); // 2～6 字
            var sb = new StringBuilder(len);
            for (var i = 0; i < len; i++)
                sb.Append(CjkPool[UnityEngine.Random.Range(0, CjkPool.Length)]);

            return sb.ToString();
        }

        static string RandomLatinName()
        {
            const string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var len = UnityEngine.Random.Range(5, 12); // 5～11，满足 >4 且 <12
            var sb = new StringBuilder(len);
            for (var i = 0; i < len; i++)
                sb.Append(letters[UnityEngine.Random.Range(0, letters.Length)]);

            return sb.ToString();
        }
    }
}
#endif
