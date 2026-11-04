using System;
using System.Collections;
using Game.Player;
using Game.Save;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>玩家携带燃料：从生产区拿起，灌注到核弹发射器；再次拿起其它燃料会合并经验。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFuelCarryController : MonoBehaviour
    {
        PlayerRig _rig;
        GameObject _visual;
        DestroyCityModeSaveData.FuelInstanceSaveEntry _carried;
        bool _restorePending;

        public bool IsCarrying => !string.IsNullOrWhiteSpace(SaveService.Data?.destroyCity?.carriedFuelInstanceId);
        public string CarriedInstanceId => SaveService.Data?.destroyCity?.carriedFuelInstanceId ?? string.Empty;
        public int CarriedFuelLevel => _carried.level;
        public float CarriedFuelXp => _carried.xp;

        public event Action<DestroyCityModeSaveData.FuelInstanceSaveEntry> PickupCompleted;
        public event Action FuelDepositCompleted;

        void Awake()
        {
            _rig = GetComponentInChildren<PlayerRig>();
        }

        void Start()
        {
            _restorePending = true;
            StartCoroutine(CoRestoreCarriedWhenReady());
        }

        IEnumerator CoRestoreCarriedWhenReady()
        {
            for (var i = 0; i < 60; i++)
            {
                if (ResolveCarrySocket() != null)
                    break;
                yield return null;
            }

            if (_restorePending)
            {
                _restorePending = false;
                RestoreCarriedFromSave();
            }
        }

        /// <summary>角色换装后由 <see cref="Game.Collectibles.Equipment.EquipmentSpawner"/> 调用，刷新挂点与展示。</summary>
        public void RefreshCarrySocketBinding()
        {
            if (!IsCarrying)
                return;

            if (_visual != null)
            {
                var socket = ResolveCarrySocket();
                if (socket != null)
                {
                    _visual.transform.SetParent(socket, false);
                    _visual.transform.localPosition = Vector3.zero;
                    _visual.transform.localRotation = Quaternion.identity;
                }
                return;
            }

            if (_carried.level > 0 || !string.IsNullOrWhiteSpace(_carried.instanceId))
                SpawnCarriedVisual(_carried.level);
            else
                RestoreCarriedFromSave();
        }

        void RestoreCarriedFromSave()
        {
            var id = CarriedInstanceId;
            if (string.IsNullOrWhiteSpace(id))
                return;

            var dc = SaveService.Data?.destroyCity;
            if (dc == null)
                return;

            if (TryLoadCarriedEntry(dc, id, out _carried))
            {
                SpawnCarriedVisual(_carried.level);
                return;
            }

            ClearCarrySave();
        }

        static bool TryLoadCarriedEntry(
            DestroyCityModeSaveData dc,
            string id,
            out DestroyCityModeSaveData.FuelInstanceSaveEntry entry)
        {
            if (dc.fuelInstances != null)
            {
                for (var i = 0; i < dc.fuelInstances.Count; i++)
                {
                    if (dc.fuelInstances[i].instanceId != id)
                        continue;
                    entry = dc.fuelInstances[i];
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(dc.carriedFuelInstanceId) || dc.carriedFuelInstanceId != id)
            {
                entry = default;
                return false;
            }

            entry = new DestroyCityModeSaveData.FuelInstanceSaveEntry
            {
                instanceId = id,
                producerId = string.Empty,
                slotIndex = 0,
                level = Mathf.Max(1, dc.carriedFuelLevel),
                xp = Mathf.Max(0f, dc.carriedFuelXp),
            };
            return true;
        }

        public bool TryPickup(FuelInstanceView fuelView)
        {
            if (fuelView == null)
                return false;

            if (!FuelProducerService.TryRemoveInstance(fuelView.InstanceId, out var picked))
                return false;

            var previousLevel = IsCarrying ? _carried.level : 0;
            if (IsCarrying)
                _carried = FuelXpMath.MergeInstances(_carried, picked);
            else
                _carried = picked;

            PersistCarriedSnapshot(_carried);
            SaveService.Save();

            if (_carried.level > previousLevel)
                DestroyCityPresentationFx.PlayFuelUpgrade(ResolveCarryFxPosition());

            SpawnCarriedVisual(_carried.level);
            Destroy(fuelView.gameObject);
            PickupCompleted?.Invoke(_carried);
            return true;
        }

        public bool TryDepositToLauncher(NukeLauncherController launcher)
        {
            if (!IsCarrying || launcher == null)
                return false;

            if (!EnsureCarriedEntryLoaded())
            {
                Debug.LogWarning("[PlayerFuelCarry] 携带燃料数据丢失，已清除携带状态。", this);
                ClearCarrySave();
                return false;
            }

            var totalXp = FuelXpMath.GetInstanceTotalXp(_carried.level, _carried.xp);
            NukeLauncherDiagnostics.Log(
                $"TryDepositToLauncher | fuelLv={_carried.level} fuelXp={_carried.xp:F0} totalXp={totalXp:F0} " +
                $"launcher={launcher.name}",
                this);
            if (totalXp <= 0f)
            {
                Debug.LogWarning(
                    $"[PlayerFuelCarry] 燃料经验为 0，无法灌注 (level={_carried.level}, xp={_carried.xp})。", this);
                return false;
            }

            if (!NukeUpgradeService.TryInfuseFuelXp(totalXp))
            {
                Debug.LogWarning("[PlayerFuelCarry] 核弹灌注失败。", this);
                return false;
            }

            DestroyCarriedVisual();
            _carried = default;
            ClearCarrySave();
            FuelDepositCompleted?.Invoke();
            launcher.RefreshNukeVisual();
            return true;
        }

        bool EnsureCarriedEntryLoaded()
        {
            var id = CarriedInstanceId;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            if (!string.IsNullOrWhiteSpace(_carried.instanceId) && _carried.instanceId == id)
                return true;

            var dc = SaveService.Data?.destroyCity;
            if (dc == null)
                return false;

            return TryLoadCarriedEntry(dc, id, out _carried);
        }

        Transform ResolveCarrySocket()
        {
            _rig ??= GetComponentInChildren<PlayerRig>();
            if (_rig == null)
                return null;

            if (_rig.carrySocket != null)
                return _rig.carrySocket;

            Transform characterRoot = null;
            if (_rig.characterSocket != null && _rig.characterSocket.childCount > 0)
                characterRoot = _rig.characterSocket.GetChild(0);
            else
                characterRoot = CharacterHierarchySockets.FindDeepChildByName(_rig.transform, "CharacterRoot");

            if (characterRoot != null)
                CharacterHierarchySockets.BindCarrySocketUnderCharacter(_rig, characterRoot);

            return _rig.carrySocket;
        }

        Vector3 ResolveCarryFxPosition()
        {
            var socket = ResolveCarrySocket();
            if (socket != null)
                return socket.position + Vector3.up * 0.5f;
            return transform.position + Vector3.up * 1.5f;
        }

        void SpawnCarriedVisual(int fuelLevel)
        {
            DestroyCarriedVisual();

            var carrySocket = ResolveCarrySocket();
            if (carrySocket == null)
                return;

            var db = Game.DestroyCity.Config.FuelDatabase.ResolveRuntime();
            GameObject prefab = null;
            if (db != null)
                prefab = db.ResolveWorldPrefabForLevel(fuelLevel);

            if (prefab == null)
            {
                _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _visual.transform.SetParent(carrySocket, false);
                _visual.transform.localScale = Vector3.one * 0.35f;
            }
            else
            {
                _visual = Instantiate(prefab, carrySocket);
                _visual.transform.localPosition = Vector3.zero;
                _visual.transform.localRotation = Quaternion.identity;
            }

            FuelInstanceVisualUtility.ConfigureCarriedVisual(_visual);
        }

        void DestroyCarriedVisual()
        {
            if (_visual != null)
                Destroy(_visual);
            _visual = null;
        }

        static void PersistCarriedSnapshot(DestroyCityModeSaveData.FuelInstanceSaveEntry entry)
        {
            var dc = SaveService.Data?.destroyCity;
            if (dc == null)
                return;

            dc.carriedFuelInstanceId = entry.instanceId;
            dc.carriedFuelLevel = Mathf.Max(1, entry.level);
            dc.carriedFuelXp = Mathf.Max(0f, entry.xp);
        }

        void ClearCarrySave()
        {
            var dc = SaveService.Data?.destroyCity;
            if (dc == null)
                return;

            dc.carriedFuelInstanceId = string.Empty;
            dc.carriedFuelLevel = 1;
            dc.carriedFuelXp = 0f;
            SaveService.Save();
        }
    }
}
