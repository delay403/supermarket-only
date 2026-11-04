using System.Collections.Generic;
using Game.AnimalHospital.Config;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>单个病房：病床、分析机、电脑、显示屏、事件锚点。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalWard : MonoBehaviour
    {
        [SerializeField] private string wardId = "ward_01";
        [SerializeField] private Transform bedPoint;
        [SerializeField] private Transform patientEnterPoint;
        [SerializeField] private AnimalHospitalInteractable analyzer;
        [SerializeField] private AnimalHospitalInteractable wardComputer;
        [SerializeField] private AnimalHospitalInteractable wardDisplay;
        [SerializeField] private Transform ceilingEventAnchor;
        [SerializeField] private Transform shadowSpawnPoint;

        [Header("火焰（可拖多个点位；着火时在此生成/显隐 FX_Fire_Big_02）")]
        [SerializeField] private GameObject[] fireVisualRoots;
        [Tooltip("留空则使用 AnimalHospitalConfig.wardFireEffectPrefab。")]
        [SerializeField] private GameObject fireEffectPrefabOverride;

        [Header("病房范围（灭火/区域判定，可在 Scene 视图编辑）")]
        [Tooltip("范围中心；留空则使用本 Ward 物体 Transform。")]
        [SerializeField] private Transform volumeCenter;
        [SerializeField] private Vector3 volumeExtents = new Vector3(4f, 1.5f, 4f);

        [Header("交互反馈 UI（场景中自建并拖入，运行时不生成）")]
        [SerializeField] private AnimalHospitalAnalyzerWorldUi analyzerUi;
        [SerializeField] private AnimalHospitalWorldProgressUi wardComputerUi;
        [SerializeField] private AnimalHospitalDisplayWorldUi wardDisplayUi;

        readonly List<GameObject> _spawnedFireEffects = new List<GameObject>();

        public string WardId => wardId;
        public Transform BedPoint => bedPoint != null ? bedPoint : transform;
        public Transform EnterPoint => patientEnterPoint != null ? patientEnterPoint : BedPoint;
        public AnimalHospitalInteractable Analyzer => analyzer;
        public AnimalHospitalInteractable WardComputer => wardComputer;
        public AnimalHospitalInteractable WardDisplay => wardDisplay;
        public Transform CeilingEventAnchor => ceilingEventAnchor != null ? ceilingEventAnchor : transform;
        public Transform ShadowSpawnPoint => shadowSpawnPoint != null ? shadowSpawnPoint : transform;
        public AnimalHospitalAnalyzerWorldUi AnalyzerUi => analyzerUi;
        public AnimalHospitalWorldProgressUi WardComputerUi => wardComputerUi;
        public AnimalHospitalDisplayWorldUi WardDisplayUi => wardDisplayUi;
        public bool IsOccupied { get; private set; }
        public AnimalHospitalPatient Occupant { get; private set; }
        public bool IsOnFire { get; private set; }

        public bool ContainsPoint(Vector3 worldPosition)
        {
            var centerTransform = volumeCenter != null ? volumeCenter : transform;
            var local = centerTransform.InverseTransformPoint(worldPosition);
            return Mathf.Abs(local.x) <= volumeExtents.x
                   && Mathf.Abs(local.y) <= volumeExtents.y
                   && Mathf.Abs(local.z) <= volumeExtents.z;
        }

        void Awake()
        {
            ClearFireEffects();
        }

        public void Occupy(AnimalHospitalPatient patient)
        {
            Occupant = patient;
            IsOccupied = patient != null;
        }

        public void ClearOccupant()
        {
            Occupant = null;
            IsOccupied = false;
        }

        public void SetOnFire(bool onFire)
        {
            IsOnFire = onFire;
            if (onFire)
                EnsureFireEffects();
            else
                ClearFireEffects();

            AnimalHospitalAudio.Resolve()?.SetWardFireLoop(onFire);
        }

        void EnsureFireEffects()
        {
            var prefab = ResolveFireEffectPrefab();
            if (prefab == null || fireVisualRoots == null || fireVisualRoots.Length == 0)
            {
                SetFireVisualRootsActive(true);
                return;
            }

            if (prefab is not GameObject prefabGo)
            {
                Debug.LogWarning(
                    $"[AnimalHospital] Ward '{wardId}' fire effect prefab is not a GameObject ({prefab.name}). " +
                    "Please reassign FX_Fire_Big_02 on AnimalHospitalConfig.",
                    this);
                SetFireVisualRootsActive(true);
                return;
            }

            for (var i = 0; i < fireVisualRoots.Length; i++)
            {
                var root = fireVisualRoots[i];
                if (root == null)
                    continue;

                root.SetActive(true);
                if (i < _spawnedFireEffects.Count && _spawnedFireEffects[i] != null)
                {
                    _spawnedFireEffects[i].SetActive(true);
                    continue;
                }

                while (_spawnedFireEffects.Count <= i)
                    _spawnedFireEffects.Add(null);

                var fx = Instantiate(prefabGo, root.transform);
                fx.transform.localPosition = Vector3.zero;
                fx.transform.localRotation = Quaternion.identity;
                fx.transform.localScale = Vector3.one;
                _spawnedFireEffects[i] = fx;
            }
        }

        void ClearFireEffects()
        {
            for (var i = _spawnedFireEffects.Count - 1; i >= 0; i--)
            {
                if (_spawnedFireEffects[i] != null)
                    Destroy(_spawnedFireEffects[i]);
            }

            _spawnedFireEffects.Clear();
            SetFireVisualRootsActive(false);
        }

        GameObject ResolveFireEffectPrefab()
        {
            if (fireEffectPrefabOverride != null)
                return fireEffectPrefabOverride;

            return AnimalHospitalConfig.ResolveRuntime()?.wardFireEffectPrefab;
        }

        void SetFireVisualRootsActive(bool active)
        {
            if (fireVisualRoots == null)
                return;

            for (var i = 0; i < fireVisualRoots.Length; i++)
            {
                var root = fireVisualRoots[i];
                if (root != null)
                    root.SetActive(active);
            }
        }

        /// <summary>优先用 Inspector 引用；未拖时尝试在 Analyzer 上取组件。</summary>
        public AnimalHospitalAnalyzerWorldUi GetAnalyzerUi()
        {
            if (analyzerUi != null)
                return analyzerUi;
            if (analyzer != null)
                analyzerUi = analyzer.GetComponent<AnimalHospitalAnalyzerWorldUi>();
            return analyzerUi;
        }

        public AnimalHospitalWorldProgressUi GetWardComputerUi()
        {
            if (wardComputerUi != null)
                return wardComputerUi;
            if (wardComputer != null)
                wardComputerUi = wardComputer.GetComponent<AnimalHospitalWorldProgressUi>();
            return wardComputerUi;
        }

        public AnimalHospitalDisplayWorldUi GetWardDisplayUi()
        {
            if (wardDisplayUi != null)
                return wardDisplayUi;
            if (wardDisplay != null)
                wardDisplayUi = wardDisplay.GetComponent<AnimalHospitalDisplayWorldUi>();
            return wardDisplayUi;
        }

        void OnDrawGizmos()
        {
            DrawVolumeGizmos(
                new Color(0.2f, 0.85f, 0.95f, 0.85f),
                new Color(0.2f, 0.85f, 0.95f, 0.1f));
            AnimalHospitalZoneGizmoUtility.DrawPoint(BedPoint, new Color(0.35f, 0.55f, 1f, 0.45f));
            AnimalHospitalZoneGizmoUtility.DrawPoint(EnterPoint, new Color(0.35f, 0.85f, 1f, 0.45f), 0.18f);
            AnimalHospitalZoneGizmoUtility.DrawPoint(CeilingEventAnchor, new Color(0.95f, 0.25f, 0.25f, 0.4f), 0.16f);
            AnimalHospitalZoneGizmoUtility.DrawPoint(ShadowSpawnPoint, new Color(0.15f, 0.15f, 0.15f, 0.55f), 0.16f);
            DrawFireGizmos(new Color(1f, 0.45f, 0.1f, 0.35f), 0.12f);
        }

        void OnDrawGizmosSelected()
        {
            DrawVolumeGizmos(
                new Color(0.2f, 0.85f, 0.95f, 1f),
                new Color(0.2f, 0.85f, 0.95f, 0.18f));
            AnimalHospitalZoneGizmoUtility.DrawPoint(BedPoint, new Color(0.35f, 0.55f, 1f, 1f), 0.26f);
            AnimalHospitalZoneGizmoUtility.DrawPoint(EnterPoint, new Color(0.35f, 0.85f, 1f, 1f), 0.22f);
            AnimalHospitalZoneGizmoUtility.DrawPoint(CeilingEventAnchor, new Color(0.95f, 0.25f, 0.25f, 1f), 0.2f);
            AnimalHospitalZoneGizmoUtility.DrawPoint(ShadowSpawnPoint, Color.black, 0.2f);
            DrawFireGizmos(new Color(1f, 0.35f, 0.05f, 1f), 0.16f);
        }

        void DrawVolumeGizmos(Color wireColor, Color fillColor)
        {
            var centerTransform = volumeCenter != null ? volumeCenter : transform;
            var half = new Vector3(
                Mathf.Max(0.05f, volumeExtents.x),
                Mathf.Max(0.05f, volumeExtents.y),
                Mathf.Max(0.05f, volumeExtents.z));

            AnimalHospitalZoneGizmoUtility.DrawPoint(centerTransform, wireColor, 0.18f);
            AnimalHospitalZoneGizmoUtility.DrawLocalXZVolume(
                centerTransform,
                Vector3.zero,
                half,
                wireColor,
                fillColor);
        }

        void DrawFireGizmos(Color color, float radius)
        {
            if (fireVisualRoots == null)
                return;
            for (var i = 0; i < fireVisualRoots.Length; i++)
            {
                if (fireVisualRoots[i] != null)
                    AnimalHospitalZoneGizmoUtility.DrawPoint(fireVisualRoots[i].transform, color, radius);
            }
        }
    }
}
