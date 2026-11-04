using Game.Core.Config;
using Game.Core.Content;
using UnityEngine;

namespace Game.SupermarketOrganize.Config
{
    [CreateAssetMenu(menuName = "Game/Supermarket Organize/Organize Config", fileName = "SupermarketOrganizeConfig")]
    public sealed class SupermarketOrganizeConfig : ScriptableObject
    {
        [Header("Handle Zone")]
        [Min(1)]
        public int defaultHandleCapacity = 16;

        [Min(1)]
        public int maxHandleCapacity = 32;

        [Tooltip("手持堆叠固定本地 X（相对 HandleZone / 摄像机）。")]
        public float handleSlotLocalX = 0.25f;

        [Tooltip("相邻物品间距 = 前一件半宽 + 间隔 + 后一件半宽。")]
        public float handleSlotGap = 0.05f;

        [Tooltip("手持区相对第一人称摄像机本地的偏移（Z 为镜头前方）。")]
        public Vector3 handleZoneLocalOffset = new Vector3(0f, -0.2f, 0.75f);

        [Header("Interaction")]
        public float raycastMaxDistance = 8f;

        [Tooltip("屏幕中心准星拾取/放置的 LayerMask。")]
        public LayerMask itemRaycastMask = ~0;

        [Tooltip("货架检测 LayerMask（可与物品相同）。")]
        public LayerMask shelfRaycastMask = ~0;

        public float tapMoveThresholdPixels = 22f;

        [Header("Highlight")]
        public Color aimHighlightColor = new Color(1f, 1f, 0.35f, 0.35f);

        public Color sameIdHighlightColor = new Color(0.35f, 0.85f, 1f, 0.35f);

        public Color sameIdOutlineColor = Color.white;

        [Range(0f, 10f)]
        public float sameIdOutlineWidth = 2.5f;

        [Tooltip("当前手持物品的外描边颜色（与准星/扫描仪描边独立）。")]
        public Color heldItemOutlineColor = new Color(0.25f, 0.95f, 0.45f, 1f);

        [Tooltip("当前手持物品的外描边宽度。")]
        [Range(0f, 10f)]
        public float heldItemOutlineWidth = 4f;

        [Header("Economy")]
        [Tooltip("每放置一个物品获得的金币。")]
        [Min(0)]
        public int coinsPerPlacedItem = 3;

        [Tooltip("每收集齐一个货架的全部物品额外获得的金币。")]
        [Min(0)]
        public int coinsPerCompletedShelf = 30;

        public Color shelfPlacePreviewColor = new Color(1f, 1f, 1f, 0.35f);

        [Header("Shelf Feedback UI")]
        public Color correctShelfTextColor = new Color(0.25f, 0.95f, 0.35f, 1f);

        public Color wrongShelfTextColor = new Color(1f, 0.28f, 0.28f, 1f);

        public string correctShelfMessage = "正确的货架";

        public string wrongShelfMessage = "错误的货架";

        public string correctShelfHintFormat = "正确货架 · 可放置 {0}";

        public string wrongShelfHintText = "错误的货架";

        [Header("Motion")]
        public float itemMoveSpeed = 6f;

        [Tooltip("拾取/放置移动的最长时间，超时强制到位。")]
        public float pickUpMoveMaxSeconds = 0.5f;

        [Tooltip("拾取/放置移动时的抛物线高度。")]
        public float pickUpArcHeight = 0.25f;

        [Header("First Person Camera")]
        public float eyeHeight = 1.55f;

        public float yawSpeed = 150f;

        public float pitchSpeed = 120f;

        public float pitchMin = -35f;

        public float pitchMax = 55f;

        [Tooltip("第一人称时隐藏玩家网格（保留碰撞与手持区）。")]
        public bool hidePlayerMeshInFirstPerson = true;

        [Header("Spawn")]
        public Vector2 scatterAreaHalfExtents = new Vector2(6f, 6f);

        [Tooltip("未启用掉落时，物品直接生成在该高度。")]
        public float scatterHeight = 0.35f;

        public int scatterSeed = 20260707;

        [Header("Drop Spawn")]
        public bool enableDropSpawn = true;

        [Tooltip("相对地面的最小生成高度（米）。与 Drop Height Max 一起控制所有物品的统一掉落起点。")]
        public float dropHeightMin = 1f;

        [Tooltip("相对地面的最大生成高度（米）。")]
        public float dropHeightMax = 1.5f;

        [Tooltip("掉落时给物品的随机水平初速度。")]
        public float dropHorizontalImpulse = 0.35f;

        [Tooltip("掉落重力倍率（1=默认重力，>1 下落更快）。")]
        public float dropGravityScale = 1f;

        [Tooltip("生成时额外向下的初速度（米/秒）。")]
        public float dropInitialDownwardSpeed = 0f;

        [Tooltip("生成时随机欧拉角范围（度）。X/Z 为 ± 倾倒；Y≥360 表示 Y 轴全向随机，否则为 ±Y。")]
        public Vector3 dropSpawnRotationRange = new Vector3(90f, 360f, 90f);

        [Tooltip("掉落开始时随机角速度上限（弧度/秒），0 表示不额外施加。")]
        [Min(0f)]
        public float dropAngularImpulse = 3f;

        [Tooltip("贴地射线只检测这些 Layer（建议仅勾选地面）。留空则检测全部。")]
        public LayerMask spawnGroundMask;

        [Tooltip("贴地射线起点世界高度（仅使用采样点 XZ）。")]
        public float spawnGroundRaycastHeight = 30f;

        [Tooltip("贴地射线最大长度。")]
        public float spawnGroundRaycastDistance = 40f;

        [Tooltip("使用固定地面高度时忽略射线。适合整层地面 Y 一致的场景。")]
        public bool useFixedSpawnFloorY;

        [Tooltip("固定地面世界 Y（仅当 Use Fixed Spawn Floor Y 勾选时生效）。")]
        public float spawnFloorWorldY;

        [Header("Drop Physics")]
        public float physicsSettleSpeedThreshold = 0.12f;

        public float physicsSettleAngularSpeedThreshold = 0.35f;

        public float physicsSettleHoldSeconds = 0.12f;

        [Tooltip("掉落超时后强制落地前的最长等待（秒）。场景较高时可适当加大。")]
        public float physicsMaxDropDurationSeconds = 8f;

        public float physicsLinearDrag = 1.2f;

        public float physicsAngularDrag = 5f;

        public float physicsSlowDampingBoost = 10f;

        public float physicsSlowAngularDampingBoost = 16f;

        [Header("Ground Item Occlusion Cull")]
        [Tooltip("对地面堆叠物品做遮挡剔除：被其它物体完全挡住时关闭 Renderer/Collider。")]
        public bool enableGroundItemOcclusionCull = true;

        [Tooltip("超过该距离（米）的地面物品直接剔除。")]
        [Min(1f)]
        public float occlusionCullMaxDistance = 14f;

        [Tooltip("遮挡检测刷新间隔（秒）。")]
        [Min(0.02f)]
        public float occlusionCullUpdateInterval = 0.12f;

        [Tooltip("每次刷新最多检测多少个地面物品。")]
        [Min(8)]
        public int occlusionCullItemsPerTick = 96;

        [Tooltip("遮挡射线 LayerMask（物品、货架、地面等）。")]
        public LayerMask occlusionCullLayerMask = ~0;

        [Tooltip("每个物体采样射线数量：1=仅中心，3=中心+左右，5=中心+四角。")]
        [Range(1, 9)]
        public int occlusionCullRaySamples = 5;

        [Tooltip("至少多少比例的采样点可见才认为物体可见（0.2 = 20%）。")]
        [Range(0.05f, 1f)]
        public float occlusionCullMinVisibleSampleRatio = 0.2f;

        [Tooltip("连续多少次判定为完全遮挡后才真正隐藏，减少移动时闪烁。")]
        [Min(1)]
        public int occlusionCullHideConfirmFrames = 3;

        [Tooltip("遮挡射线终点与采样点的距离容差（米）。")]
        [Range(0.01f, 0.2f)]
        public float occlusionCullRaySkin = 0.04f;

        [Header("Rendering Notes")]
        [TextArea(4, 10)]
        public string renderOptimizationNotes =
            "WebGL/Android 优化建议：\n" +
            "1) 同类物品共用材质与 Mesh，开启 GPU Instancing（材质 Inspector）。\n" +
            "2) 小物体关闭 Cast Shadows，仅主角色/货架投射阴影。\n" +
            "3) 描边仅对当前选中 ID 启用 QuickOutline，避免 800+ 物体常驻描边。\n" +
            "4) 静态地面/货架标记 Static 以参与 Static Batching。\n" +
            "5) 使用 URP SRP Batcher 友好 Shader；减少每物体材质变体。\n" +
            "6) 远景可给物品预制体加 LOD Group；密集堆叠区可用合并网格工具。";

        public static SupermarketOrganizeConfig ResolveRuntime() =>
            ConfigRegistry.SupermarketConfig ?? GameContentService.Resolve<SupermarketOrganizeConfig>();
    }
}
