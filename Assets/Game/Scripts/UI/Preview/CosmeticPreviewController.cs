using System.Collections;
using DG.Tweening;
using Game.Collectibles.Pets;
using Game.Collectibles.Rods;
using Game.Core;
using Game.Core.Config;
using Game.Player;
using Game.Save;
using UnityEngine;

namespace Game.UI.Preview
{
    /// <summary>
    /// RawImage / 离屏相机用的外观预览：独立 Rig + <c>PrewPlayer</c> 层，与场上主角解耦。
    /// 请在场景中挂在预览用 PlayerRig 根上，指定预览相机（仅渲染 PrewPlayer）、动画器与 motionRoot。
    /// </summary>
    public sealed class CosmeticPreviewController : MonoBehaviour
    {
        public static CosmeticPreviewController Instance { get; private set; }

        public enum CosmeticShopKind
        {
            None,
            Wing,
            Pet,
            Trail,
            Rod,
            Fish,
            CharacterHome,
        }

        [SerializeField] private PlayerRig rig;
        [SerializeField] private Camera previewCamera;
        [Tooltip("角色身体 Animator（与主角使用同一 RuntimeAnimatorController）。")]
        [SerializeField] private Animator previewAnimator;
        [Tooltip("拖尾商店左右位移的根（一般为角色模型根）。可为 CharacterRoot 本体；仅在引用丢失（换装销毁）时才会自动改指向当前模型根。")]
        [SerializeField] private Transform motionRoot;
        [Tooltip("相机 LookAt 锚点；可为场景内独立空物体、骨骼或非 CharacterRoot 子层级。仅在引用丢失时才会自动改为当前 CharacterSocket 下第一个子物体。")]
        [SerializeField] private Transform cameraLookTarget;
        [Tooltip("与主角相同的 AnimatorController 资产。")]
        [SerializeField] private RuntimeAnimatorController previewAnimatorController;

        [Header("Animator params")]
        [SerializeField] private string velocityFloatParam = "Velocity";
        [SerializeField] private string isGroundBoolParam = "IsGround";
        [Tooltip("翅膀店 / 宠物背包预览：设为 true 进入 Victory 状态（剪辑需在 Animator 内可循环或配置回程过渡）。")]
        [SerializeField] private string victoryBoolParam = "Victory";

        [Header("Camera offset (motionRoot local space)")]
        [SerializeField] private Vector3 wingCameraOffsetLocal = new Vector3(0f, 1.2f, 3.6f);
        [SerializeField] private Vector3 petCameraOffsetLocal = new Vector3(-2.4f, 2.2f, 2.8f);
        [SerializeField] private Vector3 rodCameraOffsetLocal = new Vector3(0f, 1.35f, 3.2f);
        [Tooltip("图鉴鱼预览：相对 FishRoot（或 motionRoot）的本地偏移。")]
        [SerializeField] private Vector3 fishCameraOffsetLocal = new Vector3(0f, 0.45f, 2.2f);

        [Header("Trail shop camera (绕到角色水平朝向的正面)")]
        [SerializeField] private float trailCameraFaceForwardDistance = 3.4f;
        [SerializeField] private float trailCameraFaceHeight = 1.15f;

        [Header("Trail shop")]
        [Tooltip("仅拖尾商店打开时显示（如场景装饰 / TrailGameObject）；翅膀店与宠物背包时隐藏。")]
        [SerializeField] private GameObject trailShopEnvironmentObject;
        [Tooltip("左右摆动幅度（米）。")]
        [SerializeField] private float trailStrafeAmplitude = 0.85f;
        [Tooltip("左右摆动半周期（秒），越小横向跑得越快。")]
        [SerializeField] private float trailStrafeHalfPeriodSeconds = 0.72f;
        [Tooltip("拖尾商店预览角色 Animator.speed（仅 Trail 模式）。")]
        [SerializeField] private float trailPreviewAnimatorSpeed = 1.45f;

        [Header("Camera smoothing")]
        [SerializeField] private float cameraFollowSharpness = 12f;

        CosmeticShopKind _mode = CosmeticShopKind.None;
        readonly GameObject[] _pets = new GameObject[PlayerSaveData.PetEquipSlotCount];
        GameObject _wing;
        GameObject _trail;
        GameObject _rod;
        GameObject _fish;
        GameObject _character;
        Tween _trailStrafeTween;
        Vector3 _motionRootRestLocalPos;
        Quaternion _motionRootRestLocalRot = Quaternion.identity;

        /// <summary>钓竿商店：选中行的 rodId；null 则跟存档装备。</summary>
        string _rodSelectionPreviewId;

        /// <summary>图鉴鱼预览：选中 fishId；null 则不显示鱼模型。</summary>
        string _fishSelectionPreviewId;

        /// <summary>宠物背包：选中行的配置 id；null 则各槽跟存档。</summary>
        string _petSelectionPreviewDefId;

        /// <summary>背包里选中行的实例 id（区分多只同类；解析失败时用该行配置兜底）。</summary>
        string _petSelectionPreviewInstanceId;

        /// <summary>躲避虫子主页：当前浏览的角色 id。</summary>
        string _characterHomePreviewId;

        CameraClearFlags _savedPreviewClearFlags = CameraClearFlags.SolidColor;
        Color _savedPreviewBackgroundColor = Color.clear;
        bool _savedPreviewCameraClearFromScene;

        Coroutine _deferredPreviewAnimatorRebind;

        /// <summary>供其他 UI / 逻辑二次控制预览相机（位移、RT、后处理等）。</summary>
        public Camera PreviewCamera => previewCamera;

        public CosmeticShopKind ActiveShopKind => _mode;

        private void Awake()
        {
            Instance = this;
            var layer = LayerMask.NameToLayer("PrewPlayer");
            if (layer >= 0)
                SetLayerRecursively(gameObject, layer);

            if (previewCamera != null)
            {
                if (layer >= 0)
                    previewCamera.cullingMask = 1 << layer;
                previewCamera.enabled = false;
            }

            if (motionRoot != null)
            {
                _motionRootRestLocalPos = motionRoot.localPosition;
                _motionRootRestLocalRot = motionRoot.localRotation;
            }

            if (previewAnimator != null && previewAnimatorController != null)
                previewAnimator.runtimeAnimatorController = previewAnimatorController;

            ApplyAnimatorIdle();

            CapturePreviewCameraClearDefaultsIfNeeded();
            ApplyTrailShopPresentation(false);
        }

        void CapturePreviewCameraClearDefaultsIfNeeded()
        {
            if (previewCamera == null || _savedPreviewCameraClearFromScene)
                return;
            _savedPreviewClearFlags = previewCamera.clearFlags;
            _savedPreviewBackgroundColor = previewCamera.backgroundColor;
            _savedPreviewCameraClearFromScene = true;
        }

        /// <summary>
        /// 拖尾店：显示环境物体 + Skybox；其它商店或关闭：隐藏物体并恢复进入场景时记录的纯色背景。
        /// </summary>
        void ApplyTrailShopPresentation(bool trailShopActive)
        {
            if (trailShopEnvironmentObject != null)
                trailShopEnvironmentObject.SetActive(trailShopActive);

            if (previewCamera == null)
                return;

            CapturePreviewCameraClearDefaultsIfNeeded();

            if (trailShopActive)
                previewCamera.clearFlags = CameraClearFlags.Skybox;
            else
            {
                previewCamera.clearFlags = _savedPreviewClearFlags;
                previewCamera.backgroundColor = _savedPreviewBackgroundColor;
            }
        }

        private void OnDestroy()
        {
            KillTrailTween();
            if (_deferredPreviewAnimatorRebind != null)
            {
                StopCoroutine(_deferredPreviewAnimatorRebind);
                _deferredPreviewAnimatorRebind = null;
            }

            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (_mode == CosmeticShopKind.None || previewCamera == null)
                return;

            var pivot = motionRoot != null ? motionRoot : rig != null ? rig.transform : null;
            if (pivot == null)
                return;

            Transform camPivot = pivot;
            if (_mode == CosmeticShopKind.Fish && rig != null && rig.fishSocket != null)
                camPivot = rig.fishSocket;

            var look = cameraLookTarget != null
                ? cameraLookTarget.position
                : pivot.position + Vector3.up * 1.2f;
            if (_mode == CosmeticShopKind.Fish)
                look = camPivot.position;

            var localOff = _mode switch
            {
                CosmeticShopKind.Wing => wingCameraOffsetLocal,
                CosmeticShopKind.Pet => petCameraOffsetLocal,
                CosmeticShopKind.Rod => rodCameraOffsetLocal,
                CosmeticShopKind.Fish => fishCameraOffsetLocal,
                CosmeticShopKind.CharacterHome => wingCameraOffsetLocal,
                _ => wingCameraOffsetLocal,
            };

            Vector3 desiredCamPos;
            if (_mode == CosmeticShopKind.Trail)
            {
                var flatFwd = pivot.forward;
                flatFwd.y = 0f;
                if (flatFwd.sqrMagnitude < 1e-6f)
                    flatFwd = (pivot.rotation * Vector3.forward);
                flatFwd.y = 0f;
                if (flatFwd.sqrMagnitude < 1e-6f)
                    flatFwd = Vector3.forward;
                flatFwd.Normalize();
                desiredCamPos = look + flatFwd * trailCameraFaceForwardDistance + Vector3.up * trailCameraFaceHeight;
            }
            else
                desiredCamPos = camPivot.TransformPoint(localOff);
            var dt = Time.deltaTime;
            var exp = 1f - Mathf.Exp(-cameraFollowSharpness * dt);
            previewCamera.transform.position = Vector3.Lerp(previewCamera.transform.position, desiredCamPos, exp);
            var rot = Quaternion.LookRotation(look - previewCamera.transform.position, Vector3.up);
            previewCamera.transform.rotation = Quaternion.Slerp(previewCamera.transform.rotation, rot, exp);
        }

        /// <summary>
        /// 主角装备刷新后同步 <see cref="PrewPlayer"/> 预览：与场上 <see cref="EquipmentSpawner"/> 同一存档数据源；
        /// 若正在预览翅膀/拖尾/宠物商店，会在换装后重新挂上对应预览实例。
        /// </summary>
        public static void NotifyPlayerEquipmentVisualsChanged()
        {
            if (Instance == null) return;
            Instance.ApplyEquipmentVisualsToPreview();
        }

        public void SyncPreviewCharacterWithSaveWhenIdle()
        {
            ApplyEquipmentVisualsToPreview();
        }

        /// <summary>
        /// 按存档刷新预览角色网格，并按当前商店模式恢复翅膀/拖尾/宠物预览与 Animator 参数。
        /// </summary>
        void ApplyEquipmentVisualsToPreview()
        {
            var mode = _mode;
            if (mode == CosmeticShopKind.CharacterHome)
                RebuildCharacterFromId(_characterHomePreviewId);
            else
                RebuildCharacterFromSave();

            switch (mode)
            {
                case CosmeticShopKind.Wing:
                    SpawnWing(SaveService.Data != null ? SaveService.Data.equippedWingId : "");
                    break;
                case CosmeticShopKind.Trail:
                    SpawnTrail(SaveService.Data != null ? SaveService.Data.equippedTrailId : "");
                    StartTrailStrafeTween();
                    break;
                case CosmeticShopKind.Pet:
                    RebuildPetPreviewSlots();
                    break;
                case CosmeticShopKind.Rod:
                    SpawnRod(ResolveRodPreviewId());
                    break;
                case CosmeticShopKind.Fish:
                    SpawnFish(ResolveFishPreviewId());
                    break;
                case CosmeticShopKind.CharacterHome:
                    RebuildCharacterFromId(_characterHomePreviewId);
                    ApplyAnimatorMoving();
                    break;
                default:
                    ApplyAnimatorIdle();
                    break;
            }

            if (mode != CosmeticShopKind.None)
                ApplyAnimatorForShop(mode);
        }

        public void EnterShop(CosmeticShopKind kind)
        {
            if (rig == null || previewCamera == null)
                return;

            LeaveShop();
            _mode = kind;
            previewCamera.enabled = true;
            KillTrailTween();
            RestoreMotionRootLocal();

            DestroyWingTrailPetInstances();
            if (kind == CosmeticShopKind.CharacterHome)
            {
                _characterHomePreviewId = SaveService.Data != null
                    ? SaveService.Data.equippedCharacterId
                    : null;
                RebuildCharacterFromId(_characterHomePreviewId);
            }
            else
            {
                RebuildCharacterFromSave();
            }

            ApplyAnimatorForShop(kind);

            switch (kind)
            {
                case CosmeticShopKind.Wing:
                    SpawnWing(SaveService.Data != null ? SaveService.Data.equippedWingId : "");
                    break;
                case CosmeticShopKind.Pet:
                    PetService.EnsureLoadout();
                    _petSelectionPreviewDefId = null;
                    _petSelectionPreviewInstanceId = null;
                    RebuildPetPreviewSlots();
                    break;
                case CosmeticShopKind.Trail:
                    SpawnTrail(SaveService.Data != null ? SaveService.Data.equippedTrailId : "");
                    StartTrailStrafeTween();
                    break;
                case CosmeticShopKind.Rod:
                    _rodSelectionPreviewId = null;
                    SpawnRod(ResolveRodPreviewId());
                    break;
                case CosmeticShopKind.Fish:
                    _fishSelectionPreviewId = null;
                    SpawnFish(ResolveFishPreviewId());
                    break;
                case CosmeticShopKind.CharacterHome:
                    break;
            }

            ApplyTrailShopPresentation(kind == CosmeticShopKind.Trail);
        }

        /// <summary>躲避虫子主页：开启角色预览（会动的人物模型）。</summary>
        public void EnterCharacterHomePreview()
        {
            EnterShop(CosmeticShopKind.CharacterHome);
            ApplyAnimatorMoving();
        }

        /// <summary>躲避虫子主页：切换预览角色。</summary>
        public void SetCharacterHomePreview(string characterId)
        {
            if (_mode != CosmeticShopKind.CharacterHome)
                return;

            _characterHomePreviewId = string.IsNullOrWhiteSpace(characterId) ? null : characterId.Trim();
            RebuildCharacterFromId(_characterHomePreviewId);
            ApplyAnimatorMoving();
        }

        public void LeaveShop()
        {
            KillTrailTween();
            RestoreMotionRootLocal();
            _rodSelectionPreviewId = null;
            _fishSelectionPreviewId = null;
            _petSelectionPreviewDefId = null;
            _petSelectionPreviewInstanceId = null;
            _characterHomePreviewId = null;
            _mode = CosmeticShopKind.None;
            if (previewCamera != null)
                previewCamera.enabled = false;
            DestroyWingTrailPetInstances();
            ApplyAnimatorIdle();
            ApplyTrailShopPresentation(false);
        }

        public void SetWingPreview(string wingDefId)
        {
            if (_mode != CosmeticShopKind.Wing)
                return;
            SpawnWing(wingDefId ?? "");
        }

        public void SetRodPreview(string rodDefId)
        {
            if (_mode != CosmeticShopKind.Rod)
                return;
            _rodSelectionPreviewId = string.IsNullOrWhiteSpace(rodDefId) ? null : rodDefId.Trim();
            SpawnRod(ResolveRodPreviewId());
        }

        public void SetFishPreview(string fishId)
        {
            if (_mode != CosmeticShopKind.Fish)
                return;
            _fishSelectionPreviewId = string.IsNullOrWhiteSpace(fishId) ? null : fishId.Trim();
            SpawnFish(ResolveFishPreviewId());
        }

        public void SetTrailPreview(string trailDefId)
        {
            if (_mode != CosmeticShopKind.Trail)
                return;
            SpawnTrail(trailDefId ?? "");
        }

        /// <summary>
        /// 背包里点选一行：<paramref name="petDefId"/> 为配置 id，<paramref name="backpackInstanceId"/> 为该行实例 id。
        /// 已在携带栏：三槽按存档显示（不会因「同类」误判而少模型）；未携带：有空位则预览挂在第一个空槽，已满则预览替换槽 0。
        /// 传 null/空清除选中预览。
        /// </summary>
        public void SetPetPreviewPetDefId(string petDefId, string backpackInstanceId = null)
        {
            if (_mode != CosmeticShopKind.Pet)
                return;
            _petSelectionPreviewDefId = string.IsNullOrWhiteSpace(petDefId) ? null : petDefId.Trim();
            _petSelectionPreviewInstanceId =
                string.IsNullOrWhiteSpace(backpackInstanceId) ? null : backpackInstanceId.Trim();
            RebuildPetPreviewSlots();
        }

        /// <summary>
        /// -1：不按选中项覆盖槽位（携带快照或仅实例兜底）。否则为选中宠物若立刻装备应落入的槽（与 <see cref="PetPanel.OnEquip"/> 一致）。
        /// </summary>
        static int ResolvePetSelectionPreviewSlot(string selectionDefId, string selectionInstanceId)
        {
            if (string.IsNullOrWhiteSpace(selectionDefId))
                return -1;

            PetService.EnsureLoadout();
            if (!string.IsNullOrWhiteSpace(selectionInstanceId) && PetService.IsInstanceEquipped(selectionInstanceId))
                return -1;

            var unlocked = Mathf.Min(PetService.GetUnlockedEquipSlotCount(), PlayerSaveData.PetEquipSlotCount);

            for (var slot = 0; slot < unlocked; slot++)
            {
                if (string.IsNullOrWhiteSpace(PetService.GetEquippedInstanceId(slot)))
                    return slot;
            }

            return 0;
        }

        void RebuildPetPreviewSlots()
        {
            if (_mode != CosmeticShopKind.Pet || rig == null)
                return;

            DestroyPetInstances();
            PetService.EnsureLoadout();
            var db = ConfigRegistry.Pets;
            if (db == null)
                return;

            var previewSlot =
                ResolvePetSelectionPreviewSlot(_petSelectionPreviewDefId, _petSelectionPreviewInstanceId);

            for (var slot = 0; slot < PlayerSaveData.PetEquipSlotCount; slot++)
            {
                var equippedInst = PetService.GetEquippedInstanceId(slot);
                string petId;
                if (previewSlot >= 0 && slot == previewSlot && !string.IsNullOrWhiteSpace(_petSelectionPreviewDefId))
                    petId = _petSelectionPreviewDefId;
                else
                    petId = PetService.GetEquippedPetId(slot);

                if (string.IsNullOrWhiteSpace(petId) &&
                    !string.IsNullOrWhiteSpace(equippedInst) &&
                    !string.IsNullOrWhiteSpace(_petSelectionPreviewInstanceId) &&
                    equippedInst == _petSelectionPreviewInstanceId &&
                    !string.IsNullOrWhiteSpace(_petSelectionPreviewDefId))
                    petId = _petSelectionPreviewDefId;

                if (string.IsNullOrWhiteSpace(petId))
                    continue;
                if (!db.TryGet(petId, out var def) || def == null || def.prefab == null)
                    continue;
                var parent = rig.GetPetSocket(slot);
                if (parent == null)
                    continue;

                var go = Instantiate(def.prefab, parent);
                go.transform.localRotation = Quaternion.identity;
                if (rig.PetSlotUsesSharedPetRoot(slot))
                    go.transform.localPosition = new Vector3(slot * 1.25f, 0f, 0f);
                else
                    go.transform.localPosition = Vector3.zero;
                _pets[slot] = go;
                ApplyPreviewLayerRecursive(go);
            }
        }

        void ApplyAnimatorForShop(CosmeticShopKind kind)
        {
            switch (kind)
            {
                case CosmeticShopKind.Trail:
                    ApplyAnimatorMoving();
                    break;
                case CosmeticShopKind.CharacterHome:
                    ApplyAnimatorMoving();
                    break;
                case CosmeticShopKind.Wing:
                case CosmeticShopKind.Pet:
                case CosmeticShopKind.Rod:
                case CosmeticShopKind.Fish:
                    ApplyAnimatorVictoryPreview();
                    break;
                default:
                    ApplyAnimatorIdle();
                    break;
            }
        }

        /// <summary>翅膀 / 宠物 / 钓竿商店：站立并循环 Victory（由 Animator 内 Idle↔Victory 过渡或 Victory 剪辑 Loop Time 实现循环）。</summary>
        void ApplyAnimatorVictoryPreview()
        {
            if (previewAnimator == null)
                return;
            previewAnimator.speed = 1f;
            TrySetFloat(velocityFloatParam, 0f);
            TrySetBool(isGroundBoolParam, true);
            TrySetBool(victoryBoolParam, true);
        }

        void ApplyAnimatorIdle()
        {
            if (previewAnimator == null) return;
            previewAnimator.speed = 1f;
            TrySetFloat(velocityFloatParam, 0f);
            TrySetBool(isGroundBoolParam, true);
            TrySetBool(victoryBoolParam, false);
        }

        void ApplyAnimatorMoving()
        {
            if (previewAnimator == null) return;
            TrySetBool(victoryBoolParam, false);
            if (trailPreviewAnimatorSpeed > 0.01f)
                previewAnimator.speed = trailPreviewAnimatorSpeed;
            TrySetFloat(velocityFloatParam, 1f);
            TrySetBool(isGroundBoolParam, true);
        }

        void TrySetFloat(string name, float v)
        {
            if (string.IsNullOrEmpty(name) || previewAnimator == null) return;
            foreach (var p in previewAnimator.parameters)
            {
                if (p.name == name && p.type == AnimatorControllerParameterType.Float)
                {
                    previewAnimator.SetFloat(name, v);
                    return;
                }
            }
        }

        void TrySetBool(string name, bool v)
        {
            if (string.IsNullOrEmpty(name) || previewAnimator == null) return;
            foreach (var p in previewAnimator.parameters)
            {
                if (p.name == name && p.type == AnimatorControllerParameterType.Bool)
                {
                    previewAnimator.SetBool(name, v);
                    return;
                }
            }
        }

        void StartTrailStrafeTween()
        {
            if (motionRoot == null || trailStrafeHalfPeriodSeconds <= 0.05f)
                return;
            KillTrailTween();
            motionRoot.localPosition = _motionRootRestLocalPos;
            motionRoot.localRotation = _motionRootRestLocalRot;

            var amp = trailStrafeAmplitude;
            var dur = trailStrafeHalfPeriodSeconds;
            var leftPos = _motionRootRestLocalPos + Vector3.left * amp;
            var rightPos = _motionRootRestLocalPos + Vector3.right * amp;
            var rotFaceLeft = _motionRootRestLocalRot * Quaternion.Euler(0f, -90f, 0f);
            var rotFaceRight = _motionRootRestLocalRot * Quaternion.Euler(0f, 90f, 0f);

            motionRoot.localPosition = leftPos;
            motionRoot.localRotation = rotFaceLeft;

            _trailStrafeTween = DOTween.Sequence()
                .Append(motionRoot.DOLocalMove(rightPos, dur).SetEase(Ease.InOutSine))
                .Join(motionRoot.DOLocalRotateQuaternion(rotFaceRight, dur).SetEase(Ease.InOutSine))
                .Append(motionRoot.DOLocalMove(leftPos, dur).SetEase(Ease.InOutSine))
                .Join(motionRoot.DOLocalRotateQuaternion(rotFaceLeft, dur).SetEase(Ease.InOutSine))
                .SetLoops(-1);
        }

        void KillTrailTween()
        {
            if (_trailStrafeTween != null && _trailStrafeTween.IsActive())
                _trailStrafeTween.Kill();
            _trailStrafeTween = null;
        }

        void RestoreMotionRootLocal()
        {
            if (motionRoot == null)
                return;
            motionRoot.localPosition = _motionRootRestLocalPos;
            motionRoot.localRotation = _motionRootRestLocalRot;
        }

        void RebuildCharacterFromSave()
        {
            var equippedId = SaveService.Data != null ? SaveService.Data.equippedCharacterId : null;
            RebuildCharacterFromId(equippedId);
        }

        void RebuildCharacterFromId(string characterId)
        {
            if (_deferredPreviewAnimatorRebind != null)
            {
                StopCoroutine(_deferredPreviewAnimatorRebind);
                _deferredPreviewAnimatorRebind = null;
            }

            if (_character != null)
            {
                Destroy(_character);
                _character = null;
            }

            if (rig == null || rig.characterSocket == null || string.IsNullOrWhiteSpace(characterId))
            {
                RefreshMotionAndLookTargetsIfStale();
                return;
            }

            var db = ConfigRegistry.Characters;
            if (db == null || !db.TryGet(characterId, out var def) || def == null || def.prefab == null)
            {
                RefreshMotionAndLookTargetsIfStale();
                return;
            }

            _character = Instantiate(def.prefab, rig.characterSocket);
            CharacterHierarchySockets.AlignInstantiatedCharacterNameToPrefab(_character, def.prefab);
            _character.transform.localPosition = Vector3.zero;
            _character.transform.localRotation = Quaternion.identity;
            ApplyPreviewLayerRecursive(_character);

            CharacterHierarchySockets.WarmupAnimatorsUnderInstance(_character);

            CharacterHierarchySockets.RebindAnimatorOnCharacterSocket(rig.characterSocket);

            CharacterHierarchySockets.BindWingSocketUnderCharacter(rig, _character.transform);
            CharacterHierarchySockets.BindCarrySocketUnderCharacter(rig, _character.transform);
            if (GameProductSettings.IsFishingContentEnabled)
            {
                CharacterHierarchySockets.BindRodSocketsUnderCharacter(rig, _character.transform);
                CharacterHierarchySockets.BindFishSocketUnderCharacter(rig, _character.transform);
            }

            var socketAnimator = rig.characterSocket.GetComponent<Animator>();
            if (socketAnimator != null)
                previewAnimator = socketAnimator;
            else if (previewAnimator == null)
                previewAnimator = _character.GetComponentInChildren<Animator>();

            if (previewAnimator != null && previewAnimatorController != null)
                previewAnimator.runtimeAnimatorController = previewAnimatorController;

            rig.EnsureSceneSocketsResolved();

            PlayerGameplayAnimatorBinder.BindAfterCharacterSpawn(rig);

            ScheduleDeferredPreviewAnimatorRebind(rig.characterSocket);

            RefreshMotionAndLookTargetsIfStale();
        }

        void ScheduleDeferredPreviewAnimatorRebind(Transform characterSocket)
        {
            if (!isActiveAndEnabled || characterSocket == null)
                return;
            if (_deferredPreviewAnimatorRebind != null)
                StopCoroutine(_deferredPreviewAnimatorRebind);
            _deferredPreviewAnimatorRebind = StartCoroutine(DeferredPreviewAnimatorRebindRoutine(characterSocket));
        }

        IEnumerator DeferredPreviewAnimatorRebindRoutine(Transform characterSocket)
        {
            yield return null;
            CharacterHierarchySockets.RebindAnimatorOnCharacterSocket(characterSocket);
            yield return null;
            CharacterHierarchySockets.RebindAnimatorOnCharacterSocket(characterSocket);
            _deferredPreviewAnimatorRebind = null;
            ReapplyPreviewAnimatorParametersAfterRebind();
        }

        /// <summary>
        /// 延迟 <see cref="Animator.Rebind"/> 后会重置状态；预览没有场上玩家的 Motor 每帧写 Velocity 等，在此按当前商店模式把参数写回。
        /// </summary>
        void ReapplyPreviewAnimatorParametersAfterRebind()
        {
            switch (_mode)
            {
                case CosmeticShopKind.Wing:
                case CosmeticShopKind.Pet:
                case CosmeticShopKind.Rod:
                case CosmeticShopKind.Fish:
                    ApplyAnimatorVictoryPreview();
                    break;
                case CosmeticShopKind.Trail:
                    ApplyAnimatorMoving();
                    break;
                default:
                    ApplyAnimatorIdle();
                    break;
            }
        }

        /// <summary>
        /// 仅在 Transform 引用<strong>已销毁或为空</strong>时回填默认值。
        /// 不要用「是否在 CharacterSocket 子层级」覆盖 Inspector：锚点常挂在兄弟节点 Target、或非子骨的命名骨骼上。
        /// </summary>
        void RefreshMotionAndLookTargetsIfStale()
        {
            if (rig == null || rig.characterSocket == null || rig.characterSocket.childCount == 0)
                return;

            var modelRoot = rig.characterSocket.GetChild(0);

            if (motionRoot == null || !motionRoot)
            {
                motionRoot = modelRoot;
                _motionRootRestLocalPos = motionRoot.localPosition;
                _motionRootRestLocalRot = motionRoot.localRotation;
            }

            if (cameraLookTarget == null || !cameraLookTarget)
                cameraLookTarget = modelRoot;
        }

        string ResolveRodPreviewId()
        {
            if (!string.IsNullOrWhiteSpace(_rodSelectionPreviewId))
                return _rodSelectionPreviewId.Trim();
            return SaveService.Data != null ? SaveService.Data.equippedRodId : string.Empty;
        }

        string ResolveFishPreviewId()
        {
            return _fishSelectionPreviewId ?? string.Empty;
        }

        void SpawnFish(string fishId)
        {
            if (_fish != null)
            {
                Destroy(_fish);
                _fish = null;
            }

            if (!GameProductSettings.IsFishingContentEnabled || rig == null || string.IsNullOrWhiteSpace(fishId))
                return;

            rig.EnsureSceneSocketsResolved();
            if (_character != null)
                CharacterHierarchySockets.BindFishSocketUnderCharacter(rig, _character.transform);

            if (rig.fishSocket == null)
                return;

            var db = ConfigRegistry.Fish;
            if (db == null || !db.TryGet(fishId, out var def) || def == null || def.worldCatchPrefab == null)
                return;

            _fish = Instantiate(def.worldCatchPrefab, rig.fishSocket);
            _fish.transform.localPosition = Vector3.zero;
            _fish.transform.localRotation = Quaternion.identity;
            _fish.transform.localScale = Vector3.one;
            ApplyPreviewLayerRecursive(_fish);
        }

        void SpawnRod(string rodId)
        {
            if (_rod != null)
            {
                Destroy(_rod);
                _rod = null;
            }

            if (rig == null || string.IsNullOrWhiteSpace(rodId))
                return;

            rig.EnsureSceneSocketsResolved();
            if (_character != null)
                CharacterHierarchySockets.BindRodSocketsUnderCharacter(rig, _character.transform);

            var socket = rig.rodBackSocket != null ? rig.rodBackSocket : rig.rodSocket;
            if (socket == null)
                return;

            var db = ConfigRegistry.Rods;
            if (db == null || !db.TryGet(rodId, out var def) || def == null || def.prefab == null)
                return;

            _rod = Instantiate(def.prefab, socket);
            _rod.transform.localPosition = Vector3.zero;
            _rod.transform.localRotation = Quaternion.identity;
            _rod.transform.localScale = Vector3.one;
            ApplyPreviewLayerRecursive(_rod);
        }

        void SpawnWing(string wingId)
        {
            if (_wing != null)
            {
                Destroy(_wing);
                _wing = null;
            }

            if (rig.wingSocket == null || string.IsNullOrWhiteSpace(wingId))
                return;
            var db = ConfigRegistry.Wings;
            if (db == null || !db.TryGet(wingId, out var def) || def == null || def.prefab == null)
                return;
            _wing = Instantiate(def.prefab, rig.wingSocket);
            _wing.transform.localPosition = Vector3.zero;
            _wing.transform.localRotation = Quaternion.identity;
            ApplyPreviewLayerRecursive(_wing);
        }

        void SpawnTrail(string trailId)
        {
            if (_trail != null)
            {
                Destroy(_trail);
                _trail = null;
            }

            if (rig.trailSocket == null || string.IsNullOrWhiteSpace(trailId))
                return;
            var db = ConfigRegistry.Trails;
            if (db == null || !db.TryGet(trailId, out var def) || def == null || def.prefab == null)
                return;
            _trail = Instantiate(def.prefab, rig.trailSocket);
            _trail.transform.localPosition = Vector3.zero;
            _trail.transform.localRotation = Quaternion.identity;
            ApplyPreviewLayerRecursive(_trail);
        }

        void DestroyWingTrailPetInstances()
        {
            DestroyPetInstances();
            if (_wing != null)
            {
                Destroy(_wing);
                _wing = null;
            }

            if (_trail != null)
            {
                Destroy(_trail);
                _trail = null;
            }

            if (_rod != null)
            {
                Destroy(_rod);
                _rod = null;
            }

            if (_fish != null)
            {
                Destroy(_fish);
                _fish = null;
            }
        }

        void DestroyPetInstances()
        {
            for (var i = 0; i < _pets.Length; i++)
            {
                if (_pets[i] == null) continue;
                Destroy(_pets[i]);
                _pets[i] = null;
            }
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (var i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }

        static void ApplyPreviewLayerRecursive(GameObject go)
        {
            var layer = LayerMask.NameToLayer("PrewPlayer");
            if (layer >= 0)
                SetLayerRecursively(go, layer);
        }
    }
}
