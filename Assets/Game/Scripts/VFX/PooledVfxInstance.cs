using UnityEngine;

namespace Game.VFX
{
    /// <summary>Marks pooled prefab roots so returns target the correct queue。</summary>
    internal sealed class PooledVfxInstance : MonoBehaviour
    {
        public GameplayVfxManager Owner { get; set; }

        /// <summary>Logical kind for editor debugging; pooling uses prefab queues。</summary>
        public VfxKind KindId { get; set; }

        public int PoolLaneIndex { get; set; } = -1;
    }
}
