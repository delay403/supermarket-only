using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 传送带 UV 滚动：持续改变材质 Base Map 的 <c>offset.y</c>（可选 X）。
    /// 使用 <see cref="MaterialPropertyBlock"/>，不写入共享材质资源。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConveyorBeltMaterialScroller : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;

        [Tooltip("URP 一般为 _BaseMap；Built-in 可用 _MainTex。")]
        [SerializeField] private string textureProperty = "_BaseMap";

        [Tooltip("offset.y 每秒变化量；负值反向滚动。")]
        [SerializeField] private float scrollSpeedY = 0.35f;

        [SerializeField] private bool scrollOffsetX;

        [SerializeField] private float scrollSpeedX;

        [Tooltip("将 offset 限制在 0~1，避免长时间运行浮点漂移。")]
        [SerializeField] private bool wrapOffsetTo01 = true;

        [SerializeField] private bool useUnscaledTime;

        [Tooltip("勾选后在编辑模式 Scene 视图也会预览滚动。")]
        [SerializeField] private bool animateInEditMode;

        MaterialPropertyBlock _propertyBlock;
        int _stPropertyId;
        Vector2 _tiling;
        Vector2 _offset;
        bool _initialized;

        void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
        }

        void Awake() => EnsureInitialized();

        void OnEnable() => EnsureInitialized();

        void Update()
        {
            if (!animateInEditMode && !Application.isPlaying)
                return;

            if (!EnsureInitialized())
                return;

            var dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (scrollOffsetX)
            {
                _offset.x += scrollSpeedX * dt;
                if (wrapOffsetTo01)
                    _offset.x = Mathf.Repeat(_offset.x, 1f);
            }

            _offset.y += scrollSpeedY * dt;
            if (wrapOffsetTo01)
                _offset.y = Mathf.Repeat(_offset.y, 1f);

            ApplyOffset();
        }

        bool EnsureInitialized()
        {
            if (_initialized)
                return targetRenderer != null;

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            if (targetRenderer == null)
                return false;

            _stPropertyId = Shader.PropertyToID(textureProperty + "_ST");

            var mat = targetRenderer.sharedMaterial;
            if (mat == null)
                return false;

            if (!mat.HasProperty(textureProperty))
            {
                Debug.LogWarning(
                    $"{nameof(ConveyorBeltMaterialScroller)}: 材质 {mat.name} 上未找到 {textureProperty}。",
                    this);
                return false;
            }

            _tiling = mat.GetTextureScale(textureProperty);
            _offset = mat.GetTextureOffset(textureProperty);

            _propertyBlock ??= new MaterialPropertyBlock();
            _initialized = true;
            ApplyOffset();
            return true;
        }

        void ApplyOffset()
        {
            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetVector(_stPropertyId, new Vector4(_tiling.x, _tiling.y, _offset.x, _offset.y));
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>运行时调整 Y 轴滚动速度。</summary>
        public void SetScrollSpeedY(float speed) => scrollSpeedY = speed;
    }
}
