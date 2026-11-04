using System;
using UnityEngine;

namespace Game.UI.Effects
{
    /// <summary>
    /// 金币飞入特效默认参数（可在 <see cref="UICoinFlyEffect"/> 上配置）。
    /// </summary>
    [Serializable]
    public sealed class UICoinFlySettings
    {
        [Tooltip("根据奖励金额推算的视觉金币数量下限。")]
        [Min(1)]
        public int minVisualCoins = 3;

        [Tooltip("根据奖励金额推算的视觉金币数量上限。")]
        [Min(1)]
        public int maxVisualCoins = 12;

        [Tooltip("触发后等待若干秒再开始飞入（unscaled）。")]
        [Min(0f)]
        public float startDelay;

        [Tooltip("每枚金币依次飞入的间隔（秒，unscaled）；0 表示同时飞出。")]
        [Min(0f)]
        public float coinSpawnInterval = 0.1f;

        [Tooltip("每枚金币到达目标时播放 ResourceCollect 音效。")]
        public bool playCollectSfx = true;

        [Tooltip("每枚金币到达时写入存档并刷新 HUD 金币数。")]
        public bool grantCoinsOnArrival;

        [Tooltip("ResourceCollect 音量倍率。")]
        [Min(0f)]
        public float collectSfxVolume = 1f;

        [Tooltip("生成点随机偏移半径（屏幕像素）。")]
        public Vector2 spawnOffsetRadius = new Vector2(48f, 36f);

        [Tooltip("单枚金币飞行时长（秒，unscaled）。")]
        [Min(0.05f)]
        public float moveDuration = 0.85f;

        [Tooltip("每枚金币额外随机时长（秒）。")]
        [Min(0f)]
        public float moveDurationJitter = 0.18f;

        [Tooltip("曲线侧向偏移强度（屏幕像素）。")]
        [Min(0f)]
        public float arcStrength = 90f;

        [Tooltip("侧向偏移随机范围（0~1 乘 arcStrength）。")]
        public Vector2 arcStrengthRandom = new Vector2(0.45f, 1f);

        [Tooltip("位移进度曲线（EaseOut 推荐）。")]
        public AnimationCurve moveCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.2f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("侧向弧线强度曲线（中间隆起）。")]
        public AnimationCurve arcCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f));

        [Tooltip("金币 UI 尺寸（像素）。")]
        public Vector2 coinSize = new Vector2(42f, 42f);

        [Tooltip("起始/到达时缩放。")]
        public Vector2 scaleRange = new Vector2(0.85f, 1f);
    }
}
