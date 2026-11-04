using UnityEngine;



namespace Game.AnimalHospital

{

    /// <summary>病房天花板异常：病人进房概率生成；生成后玩家在该病房内抬头会持续损失理智。</summary>

    [DisallowMultipleComponent]

    public sealed class AnimalHospitalCeilingHazard : MonoBehaviour

    {

        AnimalHospitalService _service;

        AnimalHospitalWard _ward;

        float _lookSanityTick;



        public AnimalHospitalWard Ward => _ward;



        public void Initialize(AnimalHospitalService service, AnimalHospitalWard ward)

        {

            _service = service;

            _ward = ward;

        }



        void Update()

        {

            if (_service == null || _ward == null)

                return;



            var player = _service.PlayerTransform;

            if (player == null || !_service.IsPlayerInsideWard(_ward, player.position))

            {

                _lookSanityTick = 0f;

                return;

            }



            var cam = _service.FirstPersonCamera;

            var cfg = _service.Config;

            var angle = cfg != null ? cfg.ceilingLookAngleDegrees : 60f;

            if (cam == null || !cam.LookingUpBeyond(angle))

            {

                _lookSanityTick = 0f;

                return;

            }



            _lookSanityTick += Time.deltaTime;

            if (_lookSanityTick < 1f)

                return;



            _lookSanityTick = 0f;

            _service.Sanity.ApplyDamage(cfg != null ? cfg.ceilingSanityLossPerSecond : 10f);

        }

    }

}

