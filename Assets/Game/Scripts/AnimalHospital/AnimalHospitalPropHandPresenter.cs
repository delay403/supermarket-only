using Game.AnimalHospital.Config;
using Game.Visual;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 将当前装备的道具 / 药物模型显示在摄像机前 HandleZone 中（第一人称手持）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalPropHandPresenter : MonoBehaviour
    {
        [SerializeField] private Transform handleZone;
        [SerializeField] private Vector3 handleZoneLocalOffset = new Vector3(0.3f, -0.3f, 1.1f);

        readonly System.Collections.Generic.Dictionary<AnimalHospitalPropId, AnimalHospitalPropInteractable> _propSources =
            new System.Collections.Generic.Dictionary<AnimalHospitalPropId, AnimalHospitalPropInteractable>();

        AnimalHospitalService _service;
        AnimalHospitalConfig _config;
        GameObject _spawnedHandVisual;

        public Transform HandleZone => handleZone;
        public GameObject CurrentHandVisual => _spawnedHandVisual;
        public Transform CurrentHandVisualTransform =>
            _spawnedHandVisual != null ? _spawnedHandVisual.transform : null;

        public void Configure(AnimalHospitalService service, AnimalHospitalConfig config, Camera gameplayCamera)
        {
            if (_service != null)
            {
                _service.Props.HeldPropChanged -= OnHeldPropChanged;
                _service.HeldMedicineChanged -= OnHeldMedicineChanged;
            }

            _service = service;
            _config = config;

            if (_config != null)
                handleZoneLocalOffset = _config.propHandleZoneLocalOffset;

            handleZone = EnsureHandleZone(gameplayCamera);
            RebuildPropSourceMap();
            Subscribe();
            RefreshCurrentHandVisual();
        }

        void OnDisable()
        {
            if (_service == null)
                return;

            _service.Props.HeldPropChanged -= OnHeldPropChanged;
            _service.HeldMedicineChanged -= OnHeldMedicineChanged;
        }

        void Subscribe()
        {
            if (_service == null)
                return;

            _service.Props.HeldPropChanged -= OnHeldPropChanged;
            _service.Props.HeldPropChanged += OnHeldPropChanged;
            _service.HeldMedicineChanged -= OnHeldMedicineChanged;
            _service.HeldMedicineChanged += OnHeldMedicineChanged;
        }

        void OnHeldPropChanged(AnimalHospitalPropId _) => RefreshCurrentHandVisual();

        void OnHeldMedicineChanged(string _) => RefreshCurrentHandVisual();

        void RebuildPropSourceMap()
        {
            _propSources.Clear();
            var stations = FindObjectsOfType<AnimalHospitalPropInteractable>(true);
            for (var i = 0; i < stations.Length; i++)
            {
                var station = stations[i];
                if (station == null)
                    continue;

                _propSources[station.PropId] = station;
            }
        }

        void RefreshCurrentHandVisual()
        {
            // 药物优先显示在手上；治疗后 HeldMedicine 清空则恢复道具或空手。
            if (_service != null && !string.IsNullOrEmpty(_service.HeldMedicineId))
            {
                ShowMedicineHandVisual(_service.HeldMedicineId);
                return;
            }

            var propId = _service != null ? _service.Props.HeldProp : AnimalHospitalPropId.None;
            ShowPropHandVisual(propId);
        }

        void ShowMedicineHandVisual(string medicineId)
        {
            ClearHandVisual();
            if (handleZone == null || string.IsNullOrEmpty(medicineId))
                return;

            var med = _service != null ? _service.FindMedicineDef(medicineId) : null;
            var prefab = med != null ? med.medicinePrefab : null;
            if (prefab == null)
                return;

            _spawnedHandVisual = Object.Instantiate(prefab, handleZone);
            _spawnedHandVisual.name = $"{medicineId}_HandVisual";
            var t = _spawnedHandVisual.transform;
            if (med != null)
            {
                t.localPosition = med.handLocalPosition;
                t.localRotation = Quaternion.Euler(med.handLocalEulerAngles);
                t.localScale = med.handLocalScale;
            }
            else
            {
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
            }

            DisablePhysicsAndInteraction(_spawnedHandVisual);
            InteractableEffectVisualUtility.StripEffectMaterials(_spawnedHandVisual);
        }

        void ShowPropHandVisual(AnimalHospitalPropId propId)
        {
            ClearHandVisual();
            if (propId == AnimalHospitalPropId.None || handleZone == null)
                return;

            if (!_propSources.TryGetValue(propId, out var source) || source == null)
                return;

            _spawnedHandVisual = source.CreateHandVisual(handleZone);
            if (_spawnedHandVisual != null)
                InteractableEffectVisualUtility.StripEffectMaterials(_spawnedHandVisual);
        }

        void ClearHandVisual()
        {
            if (_spawnedHandVisual == null)
                return;

            Object.Destroy(_spawnedHandVisual);
            _spawnedHandVisual = null;
        }

        static void DisablePhysicsAndInteraction(GameObject root)
        {
            if (root == null)
                return;

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            var interactables = root.GetComponentsInChildren<AnimalHospitalInteractable>(true);
            for (var i = 0; i < interactables.Length; i++)
                interactables[i].enabled = false;
        }

        Transform EnsureHandleZone(Camera gameplayCamera)
        {
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
            if (gameplayCamera == null)
                return handleZone;

            if (handleZone == null)
            {
                var existing = gameplayCamera.transform.Find("HandleZone");
                if (existing != null)
                    handleZone = existing;
            }

            if (handleZone == null)
            {
                var zoneGo = new GameObject("HandleZone");
                handleZone = zoneGo.transform;
            }

            handleZone.SetParent(gameplayCamera.transform, false);
            handleZone.localPosition = handleZoneLocalOffset;
            handleZone.localRotation = Quaternion.identity;
            handleZone.localScale = Vector3.one;
            return handleZone;
        }
    }
}
