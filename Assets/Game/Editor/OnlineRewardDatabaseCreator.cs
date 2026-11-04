#if UNITY_EDITOR
using Game.DailySignIn;
using Game.Features.OnlineReward;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>创建默认在线奖励 SO（与签到同目录）。</summary>
    public static class OnlineRewardDatabaseCreator
    {
        const string AssetPath = "Assets/Game/Shared/Cosmetics/Content/Configs/OnlineRewardDatabase.asset";

        [MenuItem("Game/Online Reward/Create Default OnlineRewardDatabase")]
        public static void CreateDefault()
        {
            var existing = AssetDatabase.LoadAssetAtPath<OnlineRewardDatabase>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog("在线奖励", "OnlineRewardDatabase 已存在。", "确定");
                return;
            }

            var db = ScriptableObject.CreateInstance<OnlineRewardDatabase>();
            db.tiers = new System.Collections.Generic.List<OnlineRewardTierDef>
            {
                NewTier(1, 60f, RewardType.Coins, 100),
                NewTier(2, 180f, RewardType.Coins, 200),
                NewTier(3, 300f, RewardType.Gems, 20),
                NewTier(4, 600f, RewardType.Coins, 500),
                NewTier(5, 900f, RewardType.Gems, 50, isGrand: true),
            };

            AssetDatabase.CreateAsset(db, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
            Debug.Log($"[OnlineReward] Created {AssetPath}");
        }

        static OnlineRewardTierDef NewTier(int number, float seconds, RewardType type, long amount, bool isGrand = false)
        {
            return new OnlineRewardTierDef
            {
                tierNumber = number,
                requiredOnlineSeconds = seconds,
                reward = new RewardDef { type = type, amount = amount },
                isGrandPrize = isGrand,
            };
        }
    }
}
#endif
