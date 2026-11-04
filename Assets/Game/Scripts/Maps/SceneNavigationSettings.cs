using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Maps
{
    /// <summary>
    /// 场景跳转策略（Resources/<see cref="ResourcesAssetName"/>.asset 可选）。
    /// 过场为 UI Mask + 纯色底图，仅对 Mask 根节点做缩放。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Maps/Scene Navigation Settings", fileName = "SceneNavigationSettings")]
    public sealed class SceneNavigationSettings : ScriptableObject
    {
        public const string ResourcesAssetName = "SceneNavigationSettings";

        [Tooltip("若勾选且场景名有效：先 LoadScene 到过场场景，再由过场异步加载目标 gameplay；否则在当前流程上直接异步加载目标场景。")]
        public bool useTransitionScene = true;

        [Tooltip("须在 Build Settings 中添加；用于中转异步加载。")]
        public string transitionSceneName = "SceneTransition";

        [Tooltip("主页场景名（进主页：NavigateToMainHome）。")]
        public string mainHomeSceneName = "MainHome";

        [Tooltip("钓鱼产品对应的 Unity 场景名（须在 Build Settings 中）。")]
        public string fishingMainSceneName = "FishScene";

        [Tooltip("摧毁城市产品对应的 Unity 场景名（须在 Build Settings 中）。")]
        public string destroyCityMainSceneName = "DestroyCityScene";

        [Tooltip("躲避虫子产品对应的 Unity 场景名（须在 Build Settings 中）。")]
        public string wormDodgeMainSceneName = "EscapeWormScene";

        [Tooltip("超市收纳产品对应的 Unity 场景名（须在 Build Settings 中）。")]
        public string supermarketOrganizeMainSceneName = "SupermarketOrganizeScene";

        [Tooltip("动物医院产品对应的 Unity 场景名（须在 Build Settings 中）。")]
        public string animalHospitalMainSceneName = "AnimalHospitalScene";

        [Tooltip(
            "为 false 时：SDK 完成后不加载主页，直接经 SceneTransition 进入 gameplay（爬塔→存档地图；仅钓鱼→FishScene）。")]
        public bool useMainHomeScene = true;

        [Tooltip("为 false 时：健康忠告页隐藏品牌标题与全屏背景（仅保留忠告与合规文案；主页为 FGUI 的渠道）。")]
        public bool showHomeTitleAndBackground = true;

        [Tooltip("可选：运行时覆盖「主页」下标题 Logo 的 Image.sprite（Editor 菜单可一键写入当前场景）。")]
        public Sprite homeTitleSprite;

        [Tooltip("可选：运行时覆盖「主页背景」的 Image.sprite。")]
        public Sprite homeBackgroundSprite;

        [Tooltip("异步加载至少展示多久（秒，unscaled），避免一闪而过。")]
        public float minimumTransitionHoldSeconds = 0.45f;

        [FormerlySerializedAs("irisMaskColor")]
        [Tooltip("底图 BG 的颜色（RGBA）。")]
        public Color transitionCoverColor = Color.black;

        [FormerlySerializedAs("irisSpriteMask")]
        [Tooltip("Mask 上图形的 Sprite（ alpha 定义镂空形状）；不填则用纯白矩形，等同全屏方形遮罩。")]
        public Sprite maskShapeSprite;

        [Tooltip("离开场景结束、进入场景开始时 Mask 的 localScale（通常为 0：完全收起）。")]
        [Min(0f)]
        public float maskScaleMin = 0f;

        [Tooltip("离开场景完成时 Mask 的 localScale（需足够大以盖住全屏，例如 20）。")]
        [Min(0f)]
        public float maskScaleMax = 20f;

        [Tooltip("遮罩放大到该进度（0-1）后再卸载当前已加载场景，避免遮罩未盖住全屏时露出空白底。")]
        [Range(0.5f, 1f)]
        public float unloadSourceSceneCoverThreshold = 0.85f;

        [FormerlySerializedAs("postLoadHoldBeforeIrisOpenSeconds")]
        [Tooltip("场景 Activate 后再等待（秒，unscaled），再执行 reveal。")]
        [Min(0f)]
        public float postLoadHoldBeforeRevealSeconds = 0.22f;

        [FormerlySerializedAs("irisCloseSeconds")]
        [Tooltip("离开：Mask scale 从 min→max 的时长（秒，unscaled）；过场最短遮罩放大时间。")]
        [SerializeField]
        float maskCloseSeconds = 1f;

        [FormerlySerializedAs("irisOpenSeconds")]
        [Tooltip("进入：Mask scale 从 max→min 的时长（秒，unscaled）；过场最短遮罩缩小时间。")]
        [SerializeField]
        float maskOpenSeconds = 1f;

        public float MaskCloseSeconds => Mathf.Max(1f, maskCloseSeconds);

        public float MaskOpenSeconds => Mathf.Max(1f, maskOpenSeconds);

        public static SceneNavigationSettings RuntimeOverride { get; set; }

        public static SceneNavigationSettings ResolveActive()
        {
            return RuntimeOverride != null ? RuntimeOverride : TryLoadFromResources();
        }

        public static SceneNavigationSettings TryLoadFromResources()
        {
            return Resources.Load<SceneNavigationSettings>(ResourcesAssetName);
        }
    }
}
