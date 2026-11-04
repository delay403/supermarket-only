using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Collectibles.Pets;
using Game.Collectibles.Stats;
using Game.Collectibles.Trails;
using Game.Collectibles.Wings;
using Game.Core;
using Game.Core.Config;
using Game.Maps;
using Game.Save;
using Game.UI.Framework;

namespace Game.Dev
{
    /// <summary>
    /// 临时 OnGUI：养成物装备/购买、抽宠物、货币与消耗展示、打印加成日志。按 F10 显示/隐藏。
    /// 编辑器与 Development Build 下会在首场景自动创建；正式包可手动挂到任意物体上调试。
    /// </summary>
    [DefaultExecutionOrder(50000)]
    public sealed class DevCollectiblesRuntimeHud : MonoBehaviour
    {
        private const float WindowWidth = 520f;
        private const float WindowHeight = 640f;

        private bool _visible;
        private Rect _windowRect;
        private Vector2 _scrollCollect;
        private Vector2 _scrollGacha;
        private int _mainTab;
        private string _lastGachaLog = string.Empty;

        private GUIStyle _titleStyle;
        private GUIStyle _wrap;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (UnityObjectFinder.FindFirst<DevCollectiblesRuntimeHud>() != null) return;
            var go = new GameObject(nameof(DevCollectiblesRuntimeHud));
            DontDestroyOnLoad(go);
            go.AddComponent<DevCollectiblesRuntimeHud>();
        }
#endif

        private void Awake()
        {
            var w = Mathf.Min(WindowWidth, Screen.width - 16f);
            var h = Mathf.Min(WindowHeight, Screen.height - 16f);
            _windowRect = new Rect(8f, 8f, w, h);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                if (GUI.Button(new Rect(8, Screen.height - 36, 120, 28), "养成HUD (F10)"))
                    _visible = true;
                return;
            }

            EnsureStyles();
            _windowRect = GUILayout.Window(192910, _windowRect, DrawWindow, "临时养成 / 抽宠 HUD  (F10 关闭)");
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("金币 / 钻石 / 消耗在下列操作中会实时扣减并 Save。", _titleStyle);

            if (SaveService.Data == null)
            {
                GUILayout.Label("SaveService 未就绪。");
                GUI.DragWindow();
                return;
            }

            var d = SaveService.Data;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"金币: {d.coins}", _titleStyle);
            GUILayout.Label($"钻石: {d.gems}", _titleStyle);
            GUILayout.Label($"奖牌: {d.medals}", _titleStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+100000 金币", GUILayout.Width(110))) { d.coins += 100000; PersistEconomy(); }
            if (GUILayout.Button("+500 钻石", GUILayout.Width(100))) { d.gems += 500; PersistEconomy(); }
            if (GUILayout.Button("+100 奖牌", GUILayout.Width(100))) { d.medals += 100; PersistEconomy(); }
            if (GUILayout.Button("打印全部加成", GUILayout.Width(120))) StatBonusDebug.LogFullBreakdown();
            if (GameProductSettings.IsFishingContentEnabled
                && GUILayout.Button("打印钓鱼力量", GUILayout.Width(120)))
                FishingStatBonusDebug.LogPowerBreakdown("DevHud");
            GUILayout.EndHorizontal();

            _mainTab = GUILayout.Toolbar(_mainTab, new[] { "宠物", "翅膀", "拖尾", "抽卡" });

            switch (_mainTab)
            {
                case 0:
                    DrawPetsTab();
                    break;
                case 1:
                    DrawWingsTab();
                    break;
                case 2:
                    DrawTrailsTab();
                    break;
                default:
                    DrawGachaTab();
                    break;
            }

            if (!string.IsNullOrEmpty(_lastGachaLog))
                GUILayout.Label(_lastGachaLog, _wrap);

            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        private void DrawPetsTab()
        {
            var db = ConfigRegistry.Pets;
            if (db == null || db.pets == null)
            {
                GUILayout.Label("PetDatabase 未加载。");
                return;
            }

            PetService.EnsureLoadout();

            GUILayout.Label("携带栏（最多 3；同宠物占多栏时需背包数量足够）", _titleStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("清空三栏", GUILayout.Width(80)))
            {
                PetService.UnequipAllPetSlots();
                AfterEquipChange();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (var s = 0; s < PetService.EquipSlotCount; s++)
            {
                var id = PetService.GetEquippedPetId(s);
                var name = string.IsNullOrEmpty(id) ? "(空)" : ResolvePetDisplay(db, id);
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.MinWidth(140f));
                GUILayout.Label($"栏{s + 1}", _titleStyle);
                GUILayout.Label(name, _wrap);
                if (GUILayout.Button("卸下本栏", GUILayout.Height(22)))
                {
                    PetService.TryEquipSlot(s, string.Empty);
                    AfterEquipChange();
                }

                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();

            GUILayout.Label("合成：三只同名同级 → 下一级（成功率在 PetDef.fusionSuccessChance01）", _titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            var anyFusion = false;
            foreach (var src in db.pets)
            {
                if (src == null) continue;
                if (!PetFusionService.CanFuseTwoToNextLevel(src.petId)) continue;
                if (!PetFusionService.TryGetFusionTarget(src.petId, out var tgt) || tgt == null) continue;
                anyFusion = true;
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    $"{src.displayName}  Lv{src.level}×2 → {tgt.displayName} Lv{tgt.level}  | 成功率 {src.fusionSuccessChance01:P0}",
                    GUILayout.ExpandWidth(true));
                GUI.enabled = PetService.GetCount(src.petId) >= 3;
                if (GUILayout.Button("合成", GUILayout.Width(56)))
                {
                    if (PetFusionService.TryFuseTwoToNextLevel(src.petId, out var resultId, out var ok))
                    {
                        Persist();
                        AfterEquipChange();
                        SetGachaLog(
                            ok
                                ? $"合成成功：{resultId}"
                                : "合成失败：已返还 1 只材料宠（另 1 只已消耗）");
                    }
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            if (!anyFusion)
                GUILayout.Label("当前无可合成宠物（需≥2只同级且库中存在下一级同品质配置）。");
            GUILayout.EndVertical();

            _scrollCollect = GUILayout.BeginScrollView(_scrollCollect, GUILayout.Height(_windowRect.height - 280f));
            foreach (var def in db.pets)
            {
                if (def == null) continue;
                var cnt = PetService.GetCount(def.petId);
                var inSlots = PetService.CountEquippedSlots(def.petId);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{def.displayName}  [{def.petId}]  品质:{def.quality}  Lv.{def.level}  持有×{cnt}" +
                                (inSlots > 0 ? $"  [栏内×{inSlots}]" : ""));
                GUILayout.Label($"蛋池:{def.hatchWeight}  币价参考:{def.hatchCostCoins}  合成率:{def.fusionSuccessChance01:P0}");

                GUILayout.BeginHorizontal();
                GUI.enabled = cnt > 0;
                for (var s = 0; s < PetService.EquipSlotCount; s++)
                {
                    if (GUILayout.Button($"栏{s + 1}", GUILayout.Width(40)))
                    {
                        if (PetService.TryEquipFirstUnequippedOfType(s, def.petId))
                        {
                            AfterEquipChange();
                            SetGachaLog($"已装备到栏 {s + 1}: {def.displayName}");
                        }
                        else
                            SetGachaLog($"无法装到栏 {s + 1}（数量不足或同宠栏位超限）");
                    }
                }

                GUI.enabled = inSlots > 0;
                if (GUILayout.Button("移出栏", GUILayout.Width(52)))
                {
                    PetService.ClearPetFromAllSlots(def.petId);
                    AfterEquipChange();
                }

                GUI.enabled = true;

#if UNITY_EDITOR
                GUI.backgroundColor = new Color(1f, 0.85f, 0.85f);
                if (GUILayout.Button("+1", GUILayout.Width(32)))
                {
                    PetService.Add(def.petId, 1);
                    Persist();
                    SetGachaLog($"[Editor] +1 {def.displayName}");
                }

                GUI.backgroundColor = Color.white;
#endif
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
        }

        private static string ResolvePetDisplay(PetDatabase db, string petId)
        {
            if (db == null || string.IsNullOrWhiteSpace(petId)) return petId ?? "";
            return db.TryGet(petId, out var d) && d != null ? $"{d.displayName} ({petId})" : petId;
        }

        private void DrawWingsTab()
        {
            _scrollCollect = GUILayout.BeginScrollView(_scrollCollect, GUILayout.Height(_windowRect.height - 160f));
            foreach (var def in WingService.GetAllSorted())
            {
                if (def == null) continue;
                var owned = WingService.IsOwned(def.wingId);
                var eq = SaveService.Data.equippedWingId == def.wingId;
                var canBuy = WingService.IsUnlockedForPurchase(def);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{def.displayName}  [{def.wingId}]  Lv.{def.level}" +
                                (owned ? " 已拥有" : " 未拥有") + (eq ? " [已装备]" : ""));
                GUILayout.Label($"购买: {def.priceCoins} 金币  |  前置等级门槛: {def.requiredUnlockedLevel}");

                GUILayout.BeginHorizontal();
                GUI.enabled = !owned && canBuy && SaveService.Data.coins >= def.priceCoins;
                if (GUILayout.Button($"购买 (-{def.priceCoins}币)", GUILayout.Width(140)))
                {
                    if (WingService.TryBuy(def.wingId))
                    {
                        Persist();
                        SetGachaLog($"购买翅膀成功: {def.displayName}，剩余金币 {SaveService.Data.coins}");
                    }
                }
                GUI.enabled = owned;
                if (GUILayout.Button("装备", GUILayout.Width(60)))
                {
                    WingService.Equip(def.wingId);
                    AfterEquipChange();
                }
                GUI.enabled = true;
                if (GUILayout.Button("卸下", GUILayout.Width(60)))
                {
                    WingService.Equip(string.Empty);
                    AfterEquipChange();
                }
                GUILayout.EndHorizontal();
                if (!canBuy && !owned)
                    GUILayout.Label("尚未满足前置解锁条件（需拥有足够等级的翅膀）。");
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
        }

        private void DrawTrailsTab()
        {
            _scrollCollect = GUILayout.BeginScrollView(_scrollCollect, GUILayout.Height(_windowRect.height - 160f));
            foreach (var def in TrailService.GetAllSorted())
            {
                if (def == null) continue;
                var owned = TrailService.IsOwned(def.trailId);
                var eq = SaveService.Data.equippedTrailId == def.trailId;
                var canBuy = TrailService.IsUnlockedForPurchase(def);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{def.displayName}  [{def.trailId}]  Lv.{def.level}" +
                                (owned ? " 已拥有" : " 未拥有") + (eq ? " [已装备]" : ""));
                GUILayout.Label($"购买: {def.priceCoins} 金币  |  前置等级门槛: {def.requiredUnlockedLevel}");

                GUILayout.BeginHorizontal();
                GUI.enabled = !owned && canBuy && SaveService.Data.coins >= def.priceCoins;
                if (GUILayout.Button($"购买 (-{def.priceCoins}币)", GUILayout.Width(140)))
                {
                    if (TrailService.TryBuy(def.trailId))
                    {
                        Persist();
                        SetGachaLog($"购买拖尾成功: {def.displayName}，剩余金币 {SaveService.Data.coins}");
                    }
                }
                GUI.enabled = owned;
                if (GUILayout.Button("装备", GUILayout.Width(60)))
                {
                    TrailService.Equip(def.trailId);
                    AfterEquipChange();
                }
                GUI.enabled = true;
                if (GUILayout.Button("卸下", GUILayout.Width(60)))
                {
                    TrailService.Equip(string.Empty);
                    AfterEquipChange();
                }
                GUILayout.EndHorizontal();
                if (!canBuy && !owned)
                    GUILayout.Label("尚未满足前置解锁条件（需拥有足够等级的拖尾）。");
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
        }

        private void DrawGachaTab()
        {
            var costCoins = PetHatchService.GetEstimatedSingleHatchCostCoins();
            var costGems = PetHatchService.PremiumSingleHatchGemCost;
            var d = SaveService.Data;

            GUILayout.Label($"金币单抽参考消耗: {costCoins}（与池内 hatchCostCoins 均值一致）");
            GUILayout.Label($"钻石单抽固定消耗: {costGems}");
            GUILayout.Label($"五连金币总消耗: {costCoins * 5}  |  五连钻石总消耗: {costGems * 5}");

            _scrollGacha = GUILayout.BeginScrollView(_scrollGacha, GUILayout.Height(220f));
            GUILayout.Label("说明：抽中结果会写入存档并叠加宠物数量；装备请在「宠物」页操作。", _wrap);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUI.enabled = d.coins >= costCoins && costCoins > 0;
            if (GUILayout.Button($"金币单抽 (-{costCoins})"))
            {
                if (PetHatchService.TryHatchOnce(out var pet) && pet != null)
                {
                    Persist();
                    SetGachaLog($"金币单抽成功: {pet.displayName} ({pet.petId})，剩余金币 {d.coins}");
                }
                else
                    SetGachaLog("金币单抽失败（金币不足或蛋池为空）。");
            }

            GUI.enabled = d.coins >= (long)costCoins * 5L && costCoins > 0;
            if (GUILayout.Button($"金币五连 (-{costCoins * 5})"))
            {
                if (PetHatchService.TryHatchFive(out var list) && list != null && list.Count > 0)
                {
                    Persist();
                    SetGachaLog(BuildMultiResult("金币五连", list, d.coins, true));
                }
                else
                    SetGachaLog("金币五连失败（金币不足或蛋池为空）。");
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = d.gems >= costGems;
            if (GUILayout.Button($"钻石单抽 (-{costGems})"))
            {
                if (PetHatchService.TryHatchOnceWithGems(out var pet) && pet != null)
                {
                    Persist();
                    SetGachaLog($"钻石单抽成功: {pet.displayName} ({pet.petId})，剩余钻石 {d.gems}");
                }
                else
                    SetGachaLog("钻石单抽失败（钻石不足或蛋池为空）。");
            }

            GUI.enabled = d.gems >= (long)costGems * 5L;
            if (GUILayout.Button($"钻石五连 (-{costGems * 5})"))
            {
                if (PetHatchService.TryHatchFiveWithGems(out var list) && list != null && list.Count > 0)
                {
                    Persist();
                    SetGachaLog(BuildMultiResult("钻石五连", list, d.gems, false));
                }
                else
                    SetGachaLog("钻石五连失败（钻石不足或蛋池为空）。");
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private static string BuildMultiResult(string title, List<PetDef> list, long currencyAfter, bool isCoins)
        {
            var sb = new StringBuilder();
            sb.Append(title).Append("：");
            for (var i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p == null) continue;
                if (i > 0) sb.Append("，");
                sb.Append(p.displayName);
            }

            sb.Append(isCoins ? " | 剩余金币 " : " | 剩余钻石 ").Append(currencyAfter);
            return sb.ToString();
        }

        private void SetGachaLog(string msg)
        {
            _lastGachaLog = msg;
            Debug.Log("[DevCollectiblesHud] " + msg);
        }

        private static void Persist()
        {
            SaveService.Save();
        }

        /// <summary>货币调试：存档并刷新主 HUD 奖牌等展示。</summary>
        private static void PersistEconomy()
        {
            SaveService.Save();
            MapRuntime.RaiseChanged();
            UiCoordinator.Hud?.RefreshEconomyDisplay();
        }

        private static void AfterEquipChange()
        {
            Persist();
            UiCoordinator.RefreshEquipmentVisuals();
        }
    }
}
