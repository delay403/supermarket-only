using System;
using Game.Features.Treadmill;
using UnityEngine;

namespace Game.UI.Framework
{
    public sealed class TreadmillUnlockOpenPayload
    {
        public TreadmillTier Tier;
        public TreadmillPanelMode Mode = TreadmillPanelMode.Unlock;
        public bool AllowFreeUnlock = true;
        public bool AllowGemUnlock = true;
        /// <summary>跑步机展示图标；留空则使用面板上的档位默认图。</summary>
        public Sprite Icon;
        /// <summary>解锁完成或确认开始后回调（将开始跑步）。</summary>
        public Action OnStartRequested;
    }
}
