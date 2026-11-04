using UnityEngine;

/// <summary>
/// 循环：局部旋转 / 往复位移 / CanvasGroup 透明度闪烁 / LocalScale 脉冲（PingPong）。
/// 单次：从 min→max 或 max→min 播放一次后停留在终点（复用各通道现有 min/max 参数）。
/// 循环与单次两种模式互斥，同一时刻仅生效一种。
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimComponent : MonoBehaviour
{
    public enum AnimMode
    {
        Loop = 0,
        OneShot = 1,
    }

    public enum OneShotDirection
    {
        /// <summary>以 min 为起点，max 为终点。</summary>
        Forward = 0,
        /// <summary>以 max 为起点，min 为终点。</summary>
        Reverse = 1,
    }

    [Header("模式（Loop 与 OneShot 互斥）")]
    public AnimMode animMode = AnimMode.Loop;
    public OneShotDirection oneShotDirection = OneShotDirection.Forward;
    [Tooltip("单次动画 min↔max 的用时（秒）。")]
    public float oneShotDuration = 0.5f;

    [Header("旋转")]
    public bool enableRotation = true;
    public bool enableMovement = false;

    public enum RotationMode { Continuous, PingPong }

    public RotationMode rotationMode = RotationMode.Continuous;

    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 90f;
    public Vector3 pingPongAngleOffset = new Vector3(0, 180, 0);
    [Tooltip("PingPong 往返一趟（到 offset 再回到起点）的用时（秒）。")]
    public float rotationPingPongDuration = 2f;

    [Header("位移")]
    public Vector3 moveOffset = new Vector3(0, 0, 5);
    [Tooltip("位移 PingPong 往返一趟的用时（秒）。")]
    public float moveDuration = 2f;

    [Header("透明度（CanvasGroup.alpha）")]
    public bool enableAlphaBlink = false;
    [Tooltip("开启时若缺少 CanvasGroup 则在运行时添加。")]
    public bool ensureCanvasGroup = true;
    [Range(0f, 1f)] public float alphaMin = 0.35f;
    [Range(0f, 1f)] public float alphaMax = 1f;
    [Tooltip("Loop：完成 min→max→min 一整圈所用时间（秒）。")]
    public float alphaBlinkPeriod = 2f;

    [Header("缩放（localScale）")]
    public bool enableScalePulse = false;
    public Vector3 scaleMin = Vector3.one * 0.92f;
    public Vector3 scaleMax = Vector3.one * 1.08f;
    [Tooltip("Loop：完成最小→最大→最小 一整圈所用时间（秒）。")]
    public float scalePulsePeriod = 2f;

    float _rotationTimer;
    float _moveTimer;
    float _alphaTimer;
    float _scaleTimer;
    float _oneShotTimer;
    bool _oneShotCompleted;

    Quaternion _baseRotation;
    Quaternion _targetRotation;
    Vector3 _basePosition;
    Vector3 _targetPosition;

    CanvasGroup _canvasGroup;

    void Awake()
    {
        CacheTransforms();
        TryGetComponent(out _canvasGroup);
        if (enableAlphaBlink && ensureCanvasGroup && _canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        CacheTransforms();
        _rotationTimer = _moveTimer = _alphaTimer = _scaleTimer = 0f;
        _oneShotTimer = 0f;
        _oneShotCompleted = false;

        if (enableAlphaBlink && ensureCanvasGroup && _canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (animMode == AnimMode.OneShot)
            ApplyOneShotState(GetOneShotT());
    }

    void CacheTransforms()
    {
        _baseRotation = transform.localRotation;
        _targetRotation = _baseRotation * Quaternion.Euler(pingPongAngleOffset);
        _basePosition = transform.localPosition;
        _targetPosition = _basePosition + moveOffset;
    }

    void Update()
    {
        if (animMode == AnimMode.OneShot)
        {
            UpdateOneShot();
            return;
        }

        UpdateLoop();
    }

    void UpdateLoop()
    {
        if (enableRotation)
        {
            if (rotationMode == RotationMode.Continuous)
                transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
            else
            {
                _rotationTimer += Time.deltaTime;
                var tr =
                    Mathf.PingPong(_rotationTimer / Mathf.Max(0.001f, rotationPingPongDuration), 1f);
                transform.localRotation = Quaternion.Slerp(_baseRotation, _targetRotation, tr);
            }
        }

        if (enableMovement)
        {
            _moveTimer += Time.deltaTime;
            var tm = Mathf.PingPong(_moveTimer / Mathf.Max(0.001f, moveDuration), 1f);
            transform.localPosition = Vector3.Lerp(_basePosition, _targetPosition, tm);
        }

        if (enableAlphaBlink)
        {
            if (_canvasGroup == null && !TryGetComponent(out _canvasGroup))
                return;
            _alphaTimer += Time.deltaTime;
            var phase = Mathf.PingPong(
                (_alphaTimer / Mathf.Max(0.001f, alphaBlinkPeriod)) * 2f,
                1f);
            _canvasGroup.alpha = Mathf.Lerp(alphaMin, alphaMax, phase);
        }

        if (enableScalePulse)
        {
            _scaleTimer += Time.deltaTime;
            var phase = Mathf.PingPong(
                (_scaleTimer / Mathf.Max(0.001f, scalePulsePeriod)) * 2f,
                1f);
            transform.localScale = Vector3.Lerp(scaleMin, scaleMax, phase);
        }
    }

    void UpdateOneShot()
    {
        if (_oneShotCompleted)
            return;

        _oneShotTimer += Time.deltaTime;
        var t = GetOneShotT();
        ApplyOneShotState(t);

        if (t >= 1f)
            _oneShotCompleted = true;
    }

    float GetOneShotT()
    {
        return Mathf.Clamp01(_oneShotTimer / Mathf.Max(0.001f, oneShotDuration));
    }

    void ApplyOneShotState(float t)
    {
        var forward = oneShotDirection == OneShotDirection.Forward;

        if (enableRotation)
        {
            var from = forward ? _baseRotation : _targetRotation;
            var to = forward ? _targetRotation : _baseRotation;
            transform.localRotation = Quaternion.Slerp(from, to, t);
        }

        if (enableMovement)
        {
            var from = forward ? _basePosition : _targetPosition;
            var to = forward ? _targetPosition : _basePosition;
            transform.localPosition = Vector3.Lerp(from, to, t);
        }

        if (enableAlphaBlink)
        {
            if (_canvasGroup == null && !TryGetComponent(out _canvasGroup))
                return;

            var from = forward ? alphaMin : alphaMax;
            var to = forward ? alphaMax : alphaMin;
            _canvasGroup.alpha = Mathf.Lerp(from, to, t);
        }

        if (enableScalePulse)
        {
            var from = forward ? scaleMin : scaleMax;
            var to = forward ? scaleMax : scaleMin;
            transform.localScale = Vector3.Lerp(from, to, t);
        }
    }
}
