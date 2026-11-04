using Game.Core;
using Game.WormDodge;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>躲避虫子局内排行榜 HUD（无弹窗、无打开按钮）。</summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeLeaderboardHud : MonoBehaviour
    {
        [SerializeField] private Text survivorCountText;
        [SerializeField] private Text rank1Text;
        [SerializeField] private Text rank2Text;
        [SerializeField] private Text rank3Text;
        [SerializeField] private Text playerRankText;

        void Awake()
        {
            TryAutoWireMissingReferences();
        }

        void OnEnable()
        {
            WormDodgeLeaderboardService.Changed += RefreshAll;
            ApplyProductModeVisibility();
            RefreshAll();
        }

        void OnDisable()
        {
            WormDodgeLeaderboardService.Changed -= RefreshAll;
        }

        public void RefreshAll()
        {
            ApplyProductModeVisibility();
            WormDodgeLeaderboardService.EnsureSeeded();

            if (survivorCountText != null)
                survivorCountText.text = $"当前存活人数：{WormDodgeLeaderboardService.SurvivorCount}";

            var entries = WormDodgeLeaderboardService.AllEntries;
            SetRankLine(rank1Text, 0, entries);
            SetRankLine(rank2Text, 1, entries);
            SetRankLine(rank3Text, 2, entries);

            if (playerRankText != null)
                playerRankText.text = $"我的排名：{WormDodgeLeaderboardService.PlayerRank}";
        }

        static void SetRankLine(Text text, int index, System.Collections.Generic.IReadOnlyList<WormDodgeLeaderboardEntry> entries)
        {
            if (text == null)
                return;

            if (entries == null || index >= entries.Count)
            {
                text.text = $"#{index + 1} ---";
                return;
            }

            var e = entries[index];
            var aliveMark = e.IsAlive ? string.Empty : " (淘汰)";
            text.text = $"#{index + 1} {e.DisplayName}  {e.Floor}层{aliveMark}";
        }

        public static void RefreshAllInScene()
        {
#if UNITY_2023_1_OR_NEWER
            var panels = FindObjectsByType<WormDodgeLeaderboardHud>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var panels = FindObjectsOfType<WormDodgeLeaderboardHud>(true);
#endif
            for (var i = 0; i < panels.Length; i++)
                panels[i].RefreshAll();
        }

        /// <summary>
        /// 为 GameHudUGUI 内嵌的旧排行榜节点自动补挂脚本（Prefab 上脚本被移除时仍能工作）。
        /// </summary>
        public static void EnsureInstalledUnder(Transform hudRoot)
        {
            if (hudRoot == null || !GameProductSettings.IsWormDodgeContentEnabled)
                return;

            var roots = new[]
            {
                hudRoot.Find("WormDodgeLeaderboardHud"),
                hudRoot.Find("WormDodgeLeaderboardPanel"),
            };

            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                var hud = root.GetComponent<WormDodgeLeaderboardHud>();
                if (hud == null)
                    hud = root.gameObject.AddComponent<WormDodgeLeaderboardHud>();

                hud.TryAutoWireMissingReferences();
                hud.RefreshAll();
            }
        }

        void TryAutoWireMissingReferences()
        {
            if (survivorCountText == null)
                survivorCountText = FindText("SurvivorCountText");
            if (rank1Text == null)
                rank1Text = FindText("Rank1Text");
            if (rank2Text == null)
                rank2Text = FindText("Rank2Text");
            if (rank3Text == null)
                rank3Text = FindText("Rank3Text");
            if (playerRankText == null)
                playerRankText = FindText("PlayerRankText");
        }

        Text FindText(string objectName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name != objectName)
                    continue;

                var text = transforms[i].GetComponent<Text>();
                if (text != null)
                    return text;
            }

            return null;
        }

        void ApplyProductModeVisibility()
        {
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.WormDodge))
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
        }
    }
}
