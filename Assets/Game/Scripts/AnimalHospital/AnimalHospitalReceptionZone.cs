using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.AnimalHospital
{
    /// <summary>接待区：接待点与接待台交互物锚点。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalReceptionZone : MonoBehaviour
    {
        [SerializeField] private Transform receptionPoint;
        [SerializeField] private Transform reportSpawnPoint;
        [SerializeField] private Transform admissionSpawnPoint;

        [Header("入院单弹出动画（相对 Admission Spawn Point）")]
        [SerializeField, Min(0.05f)] private float admissionPopDuration = 0.45f;
        [Tooltip("动画起点相对落点的偏移（世界空间）。例如 (0, 0.55, -0.15) 表示从上方落下。")]
        [SerializeField] private Vector3 admissionPopStartOffset = new Vector3(0f, 0.55f, -0.15f);

        [SerializeField] private AnimalHospitalInteractable reportInteractable;
        [SerializeField] private AnimalHospitalInteractable cameraInteractable;
        [SerializeField] private AnimalHospitalInteractable computerInteractable;
        [SerializeField] private AnimalHospitalInteractable printerInteractable;
        [SerializeField] private AnimalHospitalInteractable curtainInteractable;
        [SerializeField] private AnimalHospitalCurtainController curtainMotion;

        [Header("交互反馈 UI（场景中自建并拖入，运行时不生成）")]
        [SerializeField] private AnimalHospitalWorldProgressUi receptionComputerUi;
        [SerializeField] private GameObject photoUiRoot;
        [SerializeField] private RectTransform photoSlideRect;
        [SerializeField] private Image photoImage;
        [SerializeField] private Button closePhotoButton;

        [Header("Photo UI")]
        [Tooltip("相片在屏幕中心停留的秒数，之后滑出屏幕。")]
        [SerializeField, Min(0f)] private float photoDisplaySeconds = 3f;
        [SerializeField, Min(0.05f)] private float photoSlideDuration = 0.35f;
        [SerializeField, Min(0f)] private float photoOffscreenMargin = 40f;

        [Header("Audio")]
        [SerializeField] private AudioClip receptionBellClip;

        [Header("Camera Flash Light")]
        [Tooltip("相机 Spotlight（或任意 Light）。拍照时闪烁约 0.2 秒。")]
        [SerializeField] private Light cameraFlashLight;
        [SerializeField, Min(0.05f)] private float cameraFlashSeconds = 0.2f;

        Coroutine _photoRoutine;
        Coroutine _cameraFlashRoutine;
        bool _closeButtonBound;
        Vector2 _photoCenterAnchoredPosition;
        bool _photoCenterCached;

        public Transform ReceptionPoint => receptionPoint != null ? receptionPoint : transform;
        public Transform ReportSpawnPoint => reportSpawnPoint != null ? reportSpawnPoint : transform;
        public Transform AdmissionSpawnPoint => admissionSpawnPoint != null ? admissionSpawnPoint : transform;
        public float AdmissionPopDuration => admissionPopDuration;
        public Vector3 AdmissionPopStartOffset => admissionPopStartOffset;
        public AnimalHospitalInteractable Report => reportInteractable;
        public AnimalHospitalInteractable CameraStation => cameraInteractable;
        public AnimalHospitalInteractable Computer => computerInteractable;
        public AnimalHospitalInteractable Printer => printerInteractable;
        public AnimalHospitalInteractable Curtain => curtainInteractable;
        public bool CurtainClosed { get; private set; }
        public AnimalHospitalWorldProgressUi ReceptionComputerUi => receptionComputerUi;

        public AnimalHospitalWorldProgressUi GetReceptionComputerUi()
        {
            if (receptionComputerUi != null)
                return receptionComputerUi;
            if (computerInteractable != null)
                receptionComputerUi = computerInteractable.GetComponent<AnimalHospitalWorldProgressUi>();
            return receptionComputerUi;
        }

        public Vector3 ResolveReceptionPosition() =>
            AnimalHospitalNavMeshUtility.SampleOrKeep(ReceptionPoint.position, 20f);

        void Awake()
        {
            EnsurePhotoSlideRect();
            CachePhotoCenterPosition();
            BindClosePhotoButton();
            if (curtainMotion == null)
                curtainMotion = GetComponentInChildren<AnimalHospitalCurtainController>(true);
            if (cameraFlashLight != null)
                cameraFlashLight.enabled = false;
        }

        void EnsurePhotoSlideRect()
        {
            if (photoSlideRect != null || photoUiRoot == null)
                return;

            var photoChild = photoUiRoot.transform.Find("Photo");
            if (photoChild != null)
                photoSlideRect = photoChild as RectTransform;
        }

        void CachePhotoCenterPosition()
        {
            if (photoSlideRect == null)
                return;

            _photoCenterAnchoredPosition = photoSlideRect.anchoredPosition;
            _photoCenterCached = true;
        }

        public void PlayCameraFlash()
        {
            if (cameraFlashLight == null)
                return;

            if (_cameraFlashRoutine != null)
                StopCoroutine(_cameraFlashRoutine);
            _cameraFlashRoutine = StartCoroutine(CameraFlashRoutine());
        }

        IEnumerator CameraFlashRoutine()
        {
            cameraFlashLight.enabled = true;
            yield return new WaitForSeconds(Mathf.Max(0.05f, cameraFlashSeconds));
            cameraFlashLight.enabled = false;
            _cameraFlashRoutine = null;
        }

        public void SetCurtainClosed(bool closed)
        {
            CurtainClosed = closed;
            if (curtainInteractable != null)
                curtainInteractable.SetInteractable(true);

            if (curtainMotion == null)
                curtainMotion = GetComponentInChildren<AnimalHospitalCurtainController>(true);

            curtainMotion?.SetClosed(closed);
        }

        public void PlayReceptionBell() =>
            AnimalHospitalPresentationFx.PlayReceptionBell(ReceptionPoint.position, receptionBellClip);

        public void PlayAdmissionFormPopIn(Transform form)
        {
            if (form == null)
                return;
            AnimalHospitalSpawnMotion.PlayPopIn(
                this,
                form,
                AdmissionSpawnPoint.position,
                admissionPopDuration,
                admissionPopStartOffset);
        }

        public void ShowPhoto(Sprite sprite)
        {
            if (photoUiRoot == null || photoImage == null)
                return;

            StopPhotoRoutine();
            EnsurePhotoSlideRect();

            if (sprite == null)
            {
                HidePhotoInternal();
                return;
            }

            photoImage.sprite = sprite;
            var c = photoImage.color;
            c.a = 1f;
            photoImage.color = c;

            photoUiRoot.SetActive(true);
            BindClosePhotoButton();

            if (photoSlideRect != null)
                _photoRoutine = StartCoroutine(PhotoSlideRoutine());
        }

        public void HidePhoto()
        {
            if (photoUiRoot == null || !photoUiRoot.activeSelf)
            {
                StopPhotoRoutine();
                HidePhotoInternal();
                return;
            }

            StopPhotoRoutine();
            EnsurePhotoSlideRect();
            if (photoSlideRect != null)
                _photoRoutine = StartCoroutine(PhotoSlideOutRoutine());
            else
                HidePhotoInternal();
        }

        void BindClosePhotoButton()
        {
            if (_closeButtonBound || closePhotoButton == null)
                return;

            closePhotoButton.onClick.AddListener(HidePhoto);
            _closeButtonBound = true;
        }

        void OnDestroy()
        {
            if (closePhotoButton != null && _closeButtonBound)
                closePhotoButton.onClick.RemoveListener(HidePhoto);
        }

        void StopPhotoRoutine()
        {
            if (_photoRoutine == null)
                return;

            StopCoroutine(_photoRoutine);
            _photoRoutine = null;
        }

        void HidePhotoInternal()
        {
            if (photoSlideRect != null && _photoCenterCached)
                photoSlideRect.anchoredPosition = _photoCenterAnchoredPosition;

            if (photoUiRoot != null)
                photoUiRoot.SetActive(false);

            if (photoImage != null)
            {
                var c = photoImage.color;
                c.a = 1f;
                photoImage.color = c;
                photoImage.sprite = null;
            }
        }

        IEnumerator PhotoSlideRoutine()
        {
            if (!_photoCenterCached)
                CachePhotoCenterPosition();

            var center = _photoCenterAnchoredPosition;
            var offScreen = ResolveOffScreenAnchoredPosition();
            photoSlideRect.anchoredPosition = offScreen;

            yield return SlidePhotoTo(center, photoSlideDuration);

            if (photoDisplaySeconds > 0f)
                yield return new WaitForSeconds(photoDisplaySeconds);

            yield return SlidePhotoTo(offScreen, photoSlideDuration);
            HidePhotoInternal();
            _photoRoutine = null;
        }

        IEnumerator PhotoSlideOutRoutine()
        {
            if (!_photoCenterCached)
                CachePhotoCenterPosition();

            var offScreen = ResolveOffScreenAnchoredPosition();
            yield return SlidePhotoTo(offScreen, photoSlideDuration);
            HidePhotoInternal();
            _photoRoutine = null;
        }

        IEnumerator SlidePhotoTo(Vector2 target, float duration)
        {
            if (photoSlideRect == null)
                yield break;

            var start = photoSlideRect.anchoredPosition;
            var elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);
            while (elapsed < duration)
            {
                if (photoSlideRect == null)
                    yield break;

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                photoSlideRect.anchoredPosition = Vector2.Lerp(start, target, eased);
                yield return null;
            }

            if (photoSlideRect != null)
                photoSlideRect.anchoredPosition = target;
        }

        Vector2 ResolveOffScreenAnchoredPosition()
        {
            if (photoSlideRect == null)
                return _photoCenterAnchoredPosition;

            var parent = photoSlideRect.parent as RectTransform;
            if (parent == null)
                return _photoCenterAnchoredPosition + new Vector2(0f, -800f);

            var parentHeight = parent.rect.height;
            var photoHeight = photoSlideRect.rect.height;
            var y = -(parentHeight * 0.5f + photoHeight * 0.5f + photoOffscreenMargin);
            return new Vector2(_photoCenterAnchoredPosition.x, y);
        }

        void OnDrawGizmos()
        {
            AnimalHospitalZoneGizmoUtility.DrawPoint(ReceptionPoint, new Color(1f, 0.9f, 0.2f, 0.5f));
            AnimalHospitalZoneGizmoUtility.DrawPoint(ReportSpawnPoint, new Color(0.95f, 0.95f, 0.95f, 0.45f), 0.18f);
            AnimalHospitalZoneGizmoUtility.DrawPoint(AdmissionSpawnPoint, new Color(0.7f, 0.7f, 0.7f, 0.45f), 0.18f);
        }

        void OnDrawGizmosSelected()
        {
            AnimalHospitalZoneGizmoUtility.DrawPoint(ReceptionPoint, new Color(1f, 0.9f, 0.2f, 1f), 0.28f);
            AnimalHospitalZoneGizmoUtility.DrawPoint(ReportSpawnPoint, Color.white, 0.22f);
            AnimalHospitalZoneGizmoUtility.DrawPoint(AdmissionSpawnPoint, new Color(0.75f, 0.75f, 0.75f, 1f), 0.22f);

            if (cameraInteractable != null)
                AnimalHospitalZoneGizmoUtility.DrawPoint(cameraInteractable.transform, new Color(0.4f, 0.9f, 1f, 1f), 0.18f);
            if (computerInteractable != null)
                AnimalHospitalZoneGizmoUtility.DrawPoint(computerInteractable.transform, new Color(0.4f, 0.9f, 1f, 1f), 0.18f);
            if (printerInteractable != null)
                AnimalHospitalZoneGizmoUtility.DrawPoint(printerInteractable.transform, new Color(0.4f, 0.9f, 1f, 1f), 0.18f);
            if (curtainInteractable != null)
                AnimalHospitalZoneGizmoUtility.DrawPoint(curtainInteractable.transform, new Color(0.95f, 0.25f, 0.25f, 1f), 0.18f);
        }
    }
}
