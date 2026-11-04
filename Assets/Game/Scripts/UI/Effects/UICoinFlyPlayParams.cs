using System;
using UnityEngine;

namespace Game.UI.Effects
{
    /// <summary>
    /// 单次金币飞入播放参数；未填写的项使用 <see cref="UICoinFlyEffect"/> 上的默认配置。
    /// </summary>
    public struct UICoinFlyPlayParams
    {
        /// <summary>世界坐标起点（与 <see cref="ScreenPosition"/> 二选一）。</summary>
        public Vector3? WorldPosition;

        /// <summary>屏幕坐标起点（与 <see cref="WorldPosition"/> 二选一）。</summary>
        public Vector2? ScreenPosition;

        /// <summary>世界坐标转屏幕时使用的相机；留空则用 Camera.main。</summary>
        public Camera WorldCamera;

        /// <summary>奖励金币数量（用于推算视觉金币个数）。</summary>
        public long CoinAmount;

        /// <summary>显式指定飞行金币个数；留空则按金额推算。</summary>
        public int? VisualCoinCount;

        /// <summary>触发后等待若干秒再开始飞入；留空则用默认配置。</summary>
        public float? StartDelay;

        /// <summary>每枚金币依次飞入的间隔；留空则用默认配置。</summary>
        public float? CoinSpawnInterval;

        /// <summary>到达目标时播放 ResourceCollect；留空则用默认配置。</summary>
        public bool? PlayCollectSfx;

        /// <summary>为 true 时整次播放最多触发一次收集音效。</summary>
        public bool? PlayCollectSfxOnce;

        /// <summary>
        /// 收集音效在飞行进度中的触发点（0~1）；留空则在到达目标时与入账同时播放。
        /// 可设为略小于 1，使音效稍早于入账，减轻听感延迟。
        /// </summary>
        public float? CollectSfxMoveProgress;

        /// <summary>到达目标时入账并刷新 HUD；留空则用默认配置。</summary>
        public bool? GrantCoinsOnArrival;

        /// <summary>单枚金币到账回调（在入账之后触发）。</summary>
        public Action<long> OnCoinArrived;

        /// <summary>ResourceCollect 音量倍率；留空则用默认配置。</summary>
        public float? CollectSfxVolume;

        /// <summary>生成点随机偏移半径（屏幕像素）。</summary>
        public Vector2? SpawnOffsetRadius;

        /// <summary>飞行时长（秒）。</summary>
        public float? MoveDuration;

        /// <summary>每枚金币额外随机时长；留空则用默认配置。</summary>
        public float? MoveDurationJitter;

        /// <summary>侧向弧线强度（屏幕像素）。</summary>
        public float? ArcStrength;

        /// <summary>位移进度曲线；留空则用默认。</summary>
        public AnimationCurve MoveCurve;

        /// <summary>侧向弧线曲线；留空则用默认。</summary>
        public AnimationCurve ArcCurve;

        /// <summary>飞行目标；留空则用注册的默认目标 / UICoinFlyTarget。</summary>
        public RectTransform Target;
    }
}
