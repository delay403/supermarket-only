using Game.Audio;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 挂在场景交互 UI 按钮上：<c>Button.onClick → OnInteractPromptClicked</c>，播放 <see cref="SfxId.Interact"/>。
    /// </summary>
    public sealed class ShopEntranceInteractSound : MonoBehaviour
    {
        public void OnInteractPromptClicked() => AudioGameplayFeedback.PlayInteract();
    }
}
