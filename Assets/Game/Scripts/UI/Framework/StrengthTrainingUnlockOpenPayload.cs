using System;
using UnityEngine;

namespace Game.UI.Framework
{
    public sealed class StrengthTrainingUnlockOpenPayload
    {
        public string GroundId;
        public StrengthTrainingPanelMode Mode = StrengthTrainingPanelMode.Unlock;
        public bool AllowFreeUnlock = true;
        public bool AllowGemUnlock = true;
        /// <summary>训练场展示图标；留空则使用面板默认图或配置图标。</summary>
        public Sprite Icon;
        /// <summary>解锁完成或确认开始后回调（将开始训练）。</summary>
        public Action OnStartRequested;
    }
}
