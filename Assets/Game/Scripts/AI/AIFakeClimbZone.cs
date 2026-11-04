using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// 旧版用于 Trigger 划定攀爬区的辅助组件；当前逻辑已改为 <see cref="AIFakeClimbBehavior.climbApproachPoint"/> 寻路后再爬。
    /// 场景中可删除本组件，无运行时代码依赖。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AIFakeClimbZone : MonoBehaviour
    {
    }
}
