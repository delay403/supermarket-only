using Game.UI.Framework;
using UnityEngine;

namespace Game.UI.Util
{
    /// <summary>
    /// 挂在遮挡全屏的 UGUI 根物体上：启用时 <see cref="FairyGuiTouchGuard.PushBlock"/>（关闭 Fairy <see cref="FairyGUI.Stage"/> 对子节点的命中）。
    /// 若仍穿透到 3D/其它系统，勾选 <see cref="blockUnityPointerRaycasts"/>：对同物体上的 <see cref="UnityEngine.CanvasGroup.blocksRaycasts"/> 置真以参与 Unity GraphicRaycaster 阻挡。
    /// 注意：仅靠子节点 Mask 挡不住 FGUI HUD，因 UIPanel 与 GRoot 同为 Stage 子节点。
    /// 与 <see cref="WingShopPanel"/> 内手写 Guard 二选一时勿重复挂载，否则配对 Pop 须一致。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UgUiFairyGuiTouchBinder : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("为 true：启用时将该物体上 CanvasGroup.blocksRaycasts 设为 true（无则添加组件），防止点击传到场景或其它未参与 FGUI 的系统。")]
        bool blockUnityPointerRaycasts = true;

        CanvasGroup _canvasGroup;
        bool _ownsCanvasGroup;
        bool _savedBlocksRaycasts;

        private void OnEnable()
        {
            FairyGuiTouchGuard.PushBlock();

            if (!blockUnityPointerRaycasts)
                return;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                _ownsCanvasGroup = true;
                _savedBlocksRaycasts = false;
            }
            else if (!_ownsCanvasGroup)
                _savedBlocksRaycasts = _canvasGroup.blocksRaycasts;

            _canvasGroup.blocksRaycasts = true;
        }

        private void OnDisable()
        {
            if (blockUnityPointerRaycasts && _canvasGroup != null)
            {
                if (_ownsCanvasGroup)
                    _canvasGroup.blocksRaycasts = false;
                else
                    _canvasGroup.blocksRaycasts = _savedBlocksRaycasts;
            }

            _canvasGroup = null;
            FairyGuiTouchGuard.PopBlock();
        }
    }
}
