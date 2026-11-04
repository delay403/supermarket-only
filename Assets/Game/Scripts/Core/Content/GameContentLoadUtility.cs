using System;
using Game.UI;
using UnityEngine;

namespace Game.Maps
{
    /// <summary>内容加载提示工具。</summary>
    public static class GameContentLoadUtility
    {
        public const string ResourceLoadFailedToast = "资源加载失败，请检查网络设置";

        public static void ShowResourceLoadFailedToast(string detail = null)
        {
            var content = string.IsNullOrWhiteSpace(detail)
                ? ResourceLoadFailedToast
                : $"{ResourceLoadFailedToast}\n{detail.Trim()}";

            try
            {
                HUDMgr.ShowHUD(new HUDMessage
                {
                    Title = "提示",
                    Content = content,
                    BtnShow = true,
                    ConfirmText = "确认",
                    CloseOnConfirm = true,
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Content] 无法弹出资源失败提示: {e.Message}");
            }
        }
    }
}
