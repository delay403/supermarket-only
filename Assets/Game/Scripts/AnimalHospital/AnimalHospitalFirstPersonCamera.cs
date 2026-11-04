using Game.Player;
using Game.AnimalHospital.Config;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>动物医院第一人称镜头。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class AnimalHospitalFirstPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private LegacyPlayerInput input;
        [SerializeField] private ThirdPersonCamera thirdPersonCamera;

        float _yaw;
        float _pitch = 10f;
        float _eyeHeight = 1.55f;
        float _yawSpeed = 150f;
        float _pitchSpeed = 120f;
        float _pitchMin = -70f;
        float _pitchMax = 70f;
        bool _hidePlayerMesh;

        Renderer[] _hiddenRenderers;
        bool _playerMeshHidden;

        public float Pitch => _pitch;
        public float Yaw => _yaw;
        public bool LookingUpBeyond(float degrees) => _pitch >= degrees;

        public void Configure(
            Transform target,
            LegacyPlayerInput playerInput,
            AnimalHospitalConfig config,
            ThirdPersonCamera legacyThirdPersonCamera)
        {
            followTarget = target;
            input = playerInput != null ? playerInput : target != null ? target.GetComponent<LegacyPlayerInput>() : null;
            thirdPersonCamera = legacyThirdPersonCamera;

            if (config != null)
            {
                _eyeHeight = config.eyeHeight;
                _yawSpeed = config.yawSpeed;
                _pitchSpeed = config.pitchSpeed;
                _pitchMin = config.pitchMin;
                _pitchMax = config.pitchMax;
                _hidePlayerMesh = config.hidePlayerMeshInFirstPerson;
            }

            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                if (thirdPersonCamera != null)
                {
                    _pitch = thirdPersonCamera.transform.eulerAngles.x;
                    if (_pitch > 180f)
                        _pitch -= 360f;
                    _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);
                }
            }

            if (thirdPersonCamera != null)
                thirdPersonCamera.enabled = false;

            ApplyPlayerMeshVisibility(false);
        }

        void LateUpdate()
        {
            if (followTarget == null)
                return;

            var look = input != null ? input.Look : Vector2.zero;
            _yaw += look.x * _yawSpeed * Time.deltaTime;
            _pitch -= look.y * _pitchSpeed * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);

            var pivot = followTarget.position + Vector3.up * _eyeHeight;
            transform.position = pivot;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        void OnDisable() => ApplyPlayerMeshVisibility(true);

        void ApplyPlayerMeshVisibility(bool visible)
        {
            if (!_hidePlayerMesh || followTarget == null)
                return;

            if (!visible && !_playerMeshHidden)
            {
                _hiddenRenderers = followTarget.GetComponentsInChildren<Renderer>(true);
                for (var i = 0; i < _hiddenRenderers.Length; i++)
                {
                    if (_hiddenRenderers[i] != null)
                        _hiddenRenderers[i].enabled = false;
                }

                _playerMeshHidden = true;
            }
            else if (visible && _playerMeshHidden && _hiddenRenderers != null)
            {
                for (var i = 0; i < _hiddenRenderers.Length; i++)
                {
                    if (_hiddenRenderers[i] != null)
                        _hiddenRenderers[i].enabled = true;
                }

                _playerMeshHidden = false;
            }
        }
    }
}
