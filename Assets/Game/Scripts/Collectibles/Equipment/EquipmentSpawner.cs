using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core.Config;
using Game.Save;
using Game.Player;
using Game.Collectibles.Pets;
using Game.Collectibles.Rods;
using Game.Core;
using Game.Fishing;
using Game.Fishing.Config;
using Game.UI.Framework;
using Game.DestroyCity;

namespace Game.Collectibles.Equipment
{
    /// <summary>
    /// 根据存档在 PlayerRig 各 Socket 上实例化宠物（最多 3 栏）、翅膀、拖尾。
    /// </summary>
    public sealed class EquipmentSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerRig rig;

        private readonly GameObject[] _pets = new GameObject[PlayerSaveData.PetEquipSlotCount];
        private GameObject _wing;
        private GameObject _trail;
        private GameObject _character;
        private GameObject _rod;
        private GameObject _backFish;

        Coroutine _deferredCharacterAnimatorRebind;

        struct PetRootYawEntry
        {
            public Transform Tr;
            public Quaternion LocalRotBefore;
        }

        readonly List<PetRootYawEntry> _petGlideFallYawEntries = new List<PetRootYawEntry>(4);

        const string PetRootTransformName = "petroot";

        static bool NameLooksLikePetMountRoot(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("pet", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   name.IndexOf("root", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 优先骨骼/挂点命名 petroot；否则层级内任意「Pet*Root」子物体；再否则宠物实例父节点若像 Pet1Root；最后回退为实例根。
        /// </summary>
        static Transform ResolvePetVisualYawTransform(GameObject petInstance)
        {
            if (petInstance == null) return null;
            var root = petInstance.transform;

            var exact = FindTransformByNameRecursive(root, PetRootTransformName);
            if (exact != null) return exact;

            var bySubstring = FindTransformPetAndRootSubstring(root);
            if (bySubstring != null) return bySubstring;

            var parent = root.parent;
            if (parent != null && NameLooksLikePetMountRoot(parent.name))
                return parent;

            return root;
        }

        static Transform FindTransformPetAndRootSubstring(Transform t)
        {
            if (t == null) return null;
            if (NameLooksLikePetMountRoot(t.name)) return t;
            for (var i = 0; i < t.childCount; i++)
            {
                var hit = FindTransformPetAndRootSubstring(t.GetChild(i));
                if (hit != null) return hit;
            }

            return null;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
            RefreshAll();
        }

        private void OnEnable()
        {
            UiCoordinator.RegisterEquipment(this);
        }

        private void OnDisable()
        {
            UiCoordinator.UnregisterEquipment(this);
        }

        /// <summary>
        /// Bootstrapper 可能在其它脚本 Awake 之前调用 <see cref="RefreshAll"/>，此处保证 rig / CharacterRoot 已解析。
        /// </summary>
        void EnsureInitialized()
        {
            if (rig == null) rig = GetComponentInChildren<PlayerRig>();
            rig?.EnsureSceneSocketsResolved();
        }

        public void RefreshAll()
        {
            EnsureInitialized();
            // 翅膀挂在角色骨骼 WingRoot 下：必须先实例化角色并刷新 rig.wingSocket，再 RefreshWing。
            RefreshCharacter();
            RefreshPet();
            RefreshWing();
            RefreshTrail();
            if (GameProductSettings.IsFishingContentEnabled)
            {
                RefreshBackFish();
                if (_character == null)
                    RefreshRod();
            }
            else if (GameProductSettings.IsDestroyCityContentEnabled)
            {
                ClearFishingVisuals();
            }
        }

        void ClearFishingVisuals()
        {
            if (_rod != null)
            {
                Destroy(_rod);
                _rod = null;
            }

            if (_backFish != null)
            {
                Destroy(_backFish);
                _backFish = null;
            }
        }

        public void RefreshPet()
        {
            for (var i = 0; i < _pets.Length; i++)
            {
                if (_pets[i] == null) continue;
                Destroy(_pets[i]);
                _pets[i] = null;
            }

            if (rig == null || rig.petSocket == null) return;
            if (SaveService.Data == null) return;

            PetService.EnsureLoadout();
            var db = ConfigRegistry.Pets;
            if (db == null) return;

            for (var slot = 0; slot < PlayerSaveData.PetEquipSlotCount; slot++)
            {
                var id = PetService.GetEquippedPetId(slot);
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!db.TryGet(id, out var def) || def == null || def.prefab == null) continue;

                var parent = rig.GetPetSocket(slot);
                if (parent == null) continue;

                var go = Instantiate(def.prefab, parent);
                go.transform.localRotation = Quaternion.identity;
                if (rig.PetSlotUsesSharedPetRoot(slot))
                    go.transform.localPosition = new Vector3(slot * 1.25f, 0f, 0f);
                else
                    go.transform.localPosition = Vector3.zero;
                _pets[slot] = go;
            }

            EndPetGlideFallBackflipYawFix();
            ApplyEquipmentRootsLayers();
        }

        public void RefreshWing()
        {
            if (_wing != null) Destroy(_wing);
            if (rig == null || rig.wingSocket == null) return;
            if (SaveService.Data == null || string.IsNullOrWhiteSpace(SaveService.Data.equippedWingId)) return;

            var db = ConfigRegistry.Wings;
            if (db == null || !db.TryGet(SaveService.Data.equippedWingId, out var def) || def == null || def.prefab == null) return;
            _wing = Instantiate(def.prefab, rig.wingSocket);
            _wing.transform.localPosition = Vector3.zero;
            _wing.transform.localRotation = Quaternion.identity;
            ApplyEquipmentRootsLayers();
        }

        public void RefreshTrail()
        {
            if (_trail != null) Destroy(_trail);
            if (rig == null || rig.trailSocket == null) return;
            if (SaveService.Data == null || string.IsNullOrWhiteSpace(SaveService.Data.equippedTrailId)) return;

            var db = ConfigRegistry.Trails;
            if (db == null || !db.TryGet(SaveService.Data.equippedTrailId, out var def) || def == null || def.prefab == null) return;
            _trail = Instantiate(def.prefab, rig.trailSocket);
            _trail.transform.localPosition = Vector3.zero;
            _trail.transform.localRotation = Quaternion.identity;
            ApplyEquipmentRootsLayers();
        }

        public void RefreshBackFish()
        {
            if (_backFish != null)
            {
                Destroy(_backFish);
                _backFish = null;
            }

            if (!GameProductSettings.IsFishingContentEnabled)
                return;

            if (_character != null && rig != null)
                CharacterHierarchySockets.BindFishSocketUnderCharacter(rig, _character.transform);

            if (rig == null || rig.fishSocket == null)
                return;

            var fishId = FishCodexEquipService.GetEquippedBackFishId();
            if (string.IsNullOrWhiteSpace(fishId))
                return;

            var db = ConfigRegistry.Fish;
            if (db == null || !db.TryGet(fishId, out var def) || def == null || def.worldCatchPrefab == null)
                return;

            _backFish = Instantiate(def.worldCatchPrefab, rig.fishSocket);
            _backFish.transform.localPosition = Vector3.zero;
            _backFish.transform.localRotation = Quaternion.identity;
            _backFish.transform.localScale = Vector3.one;
            ApplyEquipmentRootsLayers();
        }

        public void RefreshRod()
        {
            if (_rod != null) Destroy(_rod);
            var backSocket = ResolveRodBackSocket();
            if (rig == null || backSocket == null) return;
            if (SaveService.Data == null || string.IsNullOrWhiteSpace(SaveService.Data.equippedRodId)) return;

            var db = ConfigRegistry.Rods;
            if (db == null || !db.TryGet(SaveService.Data.equippedRodId, out var def) || def == null || def.prefab == null)
                return;

            _rod = Instantiate(def.prefab, backSocket);
            ApplyRodLocalIdentity(_rod.transform);
            _rodMountOnHand = false;
            ApplyEquipmentRootsLayers();
        }

        /// <summary>收竿：挂到背部 BackRodRoot。</summary>
        public void MountRodOnBack()
        {
            ApplyRodMount(hand: false);
        }

        /// <summary>持竿：挂到右手 HandRodRoot。</summary>
        public void MountRodOnHand()
        {
            ApplyRodMount(hand: true);
        }

        public GameObject RodInstance => _rod;

        bool _rodMountOnHand;

        void ApplyRodMount(bool hand)
        {
            if (_rod == null)
                return;

            if (_character != null && rig != null)
                CharacterHierarchySockets.BindRodSocketsUnderCharacter(rig, _character.transform);

            var socket = hand ? ResolveRodHandSocket() : ResolveRodBackSocket();
            if (socket == null)
            {
                if (hand)
                    Debug.LogWarning("[EquipmentSpawner] 未找到 HandRodRoot，钓竿保持背部挂点。", this);
                return;
            }

            if (_rod.transform.parent == socket && _rodMountOnHand == hand)
                return;

            _rod.transform.SetParent(socket, false);
            ApplyRodLocalIdentity(_rod.transform);
            _rodMountOnHand = hand;
            ApplyEquipmentRootsLayers();
        }

        static void ApplyRodLocalIdentity(Transform rodTransform)
        {
            if (rodTransform == null) return;
            rodTransform.localPosition = Vector3.zero;
            rodTransform.localRotation = Quaternion.identity;
            rodTransform.localScale = Vector3.one;
        }

        Transform ResolveRodBackSocket()
        {
            if (rig == null) return null;
            if (rig.rodBackSocket != null) return rig.rodBackSocket;
            return rig.rodSocket;
        }

        Transform ResolveRodHandSocket()
        {
            if (rig == null) return null;
            if (rig.rodHandSocket != null) return rig.rodHandSocket;
            return ResolveRodBackSocket();
        }

        public void RefreshCharacter()
        {
            EnsureInitialized();

            // 翅膀若挂在旧角色 WingRoot 下，会先随角色销毁；先清引用以免 Missing。
            if (_wing != null)
            {
                Destroy(_wing);
                _wing = null;
            }

            if (_rod != null)
            {
                Destroy(_rod);
                _rod = null;
            }

            if (_backFish != null)
            {
                Destroy(_backFish);
                _backFish = null;
            }

            if (_character != null) Destroy(_character);
            _character = null;

            if (rig == null || rig.characterSocket == null)
            {
                Debug.LogWarning(
                    "[EquipmentSpawner] 未找到角色挂点：请在 Player 层级下放置名为 CharacterRoot 的 Transform，或在 PlayerRig.characterSocket 上手动指定。",
                    this);
                return;
            }

            if (SaveService.Data == null || string.IsNullOrWhiteSpace(SaveService.Data.equippedCharacterId))
            {
                Debug.LogWarning("[EquipmentSpawner] 存档未指定 equippedCharacterId，跳过角色实例化。", this);
                return;
            }

            var db = ConfigRegistry.Characters;
            if (db == null || !db.TryGet(SaveService.Data.equippedCharacterId, out var def) || def == null || def.prefab == null)
            {
                Debug.LogWarning(
                    $"[EquipmentSpawner] 无法加载角色 '{SaveService.Data.equippedCharacterId}'：CharacterDatabase 缺少条目或 prefab。",
                    this);
                return;
            }
            _character = Instantiate(def.prefab, rig.characterSocket);
            CharacterHierarchySockets.AlignInstantiatedCharacterNameToPrefab(_character, def.prefab);
            _character.transform.localPosition = Vector3.zero;
            _character.transform.localRotation = Quaternion.identity;

            WarmupCharacterAnimators(_character);
            CharacterHierarchySockets.RebindAnimatorOnCharacterSocket(rig.characterSocket);

            CharacterHierarchySockets.BindWingSocketUnderCharacter(rig, _character.transform);
            CharacterHierarchySockets.BindCarrySocketUnderCharacter(rig, _character.transform);
            if (GameProductSettings.IsFishingContentEnabled)
            {
                CharacterHierarchySockets.BindRodSocketsUnderCharacter(rig, _character.transform);
                CharacterHierarchySockets.BindFishSocketUnderCharacter(rig, _character.transform);
                CharacterHierarchySockets.BindHandFishSocketUnderCharacter(rig, _character.transform);
                CharacterHierarchySockets.BindFootSocketUnderCharacter(rig, _character.transform);
            }

            ApplyEquipmentRootsLayers();
            PlayerGameplayAnimatorBinder.BindAfterCharacterSpawn(rig);
            RefreshPlayerFuelCarryBinding();

            if (GameProductSettings.IsFishingContentEnabled)
            {
                RefreshRod();
                RefreshBackFish();
            }

            if (_deferredCharacterAnimatorRebind != null)
            {
                StopCoroutine(_deferredCharacterAnimatorRebind);
                _deferredCharacterAnimatorRebind = null;
            }

            _deferredCharacterAnimatorRebind = StartCoroutine(DeferredRebindCharacterAnimatorRoutine(rig.characterSocket));
        }

        void RefreshPlayerFuelCarryBinding()
        {
            if (!GameProductSettings.IsDestroyCityContentEnabled)
                return;

            var carry = GetComponentInChildren<PlayerFuelCarryController>(true);
            carry?.RefreshCarrySocketBinding();
        }

        IEnumerator DeferredRebindCharacterAnimatorRoutine(Transform characterSocket)
        {
            yield return null;
            CharacterHierarchySockets.RebindAnimatorOnCharacterSocket(characterSocket);
            yield return null;
            CharacterHierarchySockets.RebindAnimatorOnCharacterSocket(characterSocket);
            _deferredCharacterAnimatorRebind = null;
        }

        /// <summary>
        /// 运行时实例化后刷新 Humanoid 绑定与首帧姿态（手动拖预制体到场景时 Unity 已做过等价初始化）。
        /// </summary>
        static void WarmupCharacterAnimators(GameObject characterInstance)
        {
            CharacterHierarchySockets.WarmupAnimatorsUnderInstance(characterInstance);
        }

        /// <summary>
        /// Instantiate 后把 WingRoot / TrailRoot / Pet*Root 子树下<strong>所有层级</strong>设为 Player。
        /// </summary>
        private void ApplyEquipmentRootsLayers()
        {
            if (rig == null) return;
            PlayerEquipmentRootsLayer.ApplyToHierarchy(rig.transform.root);
        }

        /// <summary>
        /// 后空翻接入的滑翔<strong>坠落段</strong>：在名为 petroot 的子节点上叠加本地 Yaw +180°（相对当前本地旋转），用于修正动画朝向与玩家 Transform 不一致时的宠物朝向。
        /// </summary>
        public bool TryBeginPetGlideFallBackflipYawFix()
        {
            EndPetGlideFallBackflipYawFix();
            _petGlideFallYawEntries.Clear();

            for (var i = 0; i < _pets.Length; i++)
            {
                if (_pets[i] == null) continue;
                var pr = ResolvePetVisualYawTransform(_pets[i]);
                if (pr == null) continue;
                _petGlideFallYawEntries.Add(new PetRootYawEntry { Tr = pr, LocalRotBefore = pr.localRotation });
                pr.localRotation = pr.localRotation * Quaternion.Euler(0f, 180f, 0f);
            }

            return _petGlideFallYawEntries.Count > 0;
        }

        public void EndPetGlideFallBackflipYawFix()
        {
            for (var i = 0; i < _petGlideFallYawEntries.Count; i++)
            {
                var e = _petGlideFallYawEntries[i];
                if (e.Tr != null) e.Tr.localRotation = e.LocalRotBefore;
            }

            _petGlideFallYawEntries.Clear();
        }

        static Transform FindTransformByNameRecursive(Transform t, string name)
        {
            if (t == null || string.IsNullOrEmpty(name)) return null;
            if (t.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return t;
            for (var i = 0; i < t.childCount; i++)
            {
                var c = FindTransformByNameRecursive(t.GetChild(i), name);
                if (c != null) return c;
            }

            return null;
        }
    }
}
