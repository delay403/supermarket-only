using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>UGUI 按钮按住桥接。</summary>
    public sealed class AnimalHospitalHoldButtonBridge : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler,
        UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        AnimalHospitalHudSection _hud;

        public void Bind(AnimalHospitalHudSection hud) => _hud = hud;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData) =>
            _hud?.SetHudHolding(true);

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData) =>
            _hud?.SetHudHolding(false);

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) =>
            _hud?.SetHudHolding(false);

        void OnDisable() => _hud?.SetHudHolding(false);
    }
}
