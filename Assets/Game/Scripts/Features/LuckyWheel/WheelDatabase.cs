using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.LuckyWheel
{
    [CreateAssetMenu(menuName = "Game/Lucky Wheel/WheelDatabase", fileName = "WheelDatabase")]
    public sealed class WheelDatabase : ScriptableObject
    {
        [Tooltip("须为 8 格；顺序对应场景中转盘父子物体序号（0 为指针 12 点钟起始格，顺时针递增）。")]
        public List<WheelSliceDef> slices = new List<WheelSliceDef>();

        public int SliceCount => slices != null ? slices.Count : 0;

        public WheelSliceDef GetSlice(int index)
        {
            if (slices == null || slices.Count == 0) return null;
            var i = ((index % slices.Count) + slices.Count) % slices.Count;
            return slices[i];
        }

        /// <summary>
        /// 按各格 <see cref="WheelSliceDef.weight"/> 加权随机。
        /// 权重未配置或 ≤0 时按 1，兼容旧资源。
        /// </summary>
        /// <param name="sliceCountOverride">仅在前 N 格中抽取（与转盘实际格数对齐）；≤0 则用全部。</param>
        public int PickWeightedIndex(int sliceCountOverride = -1)
        {
            if (slices == null || slices.Count == 0)
                return 0;

            var count = sliceCountOverride > 0
                ? Mathf.Min(sliceCountOverride, slices.Count)
                : slices.Count;
            if (count <= 0)
                return 0;

            var total = 0;
            for (var i = 0; i < count; i++)
                total += ResolveWeight(slices[i]);

            if (total <= 0)
                return Random.Range(0, count);

            var roll = Random.Range(0, total);
            var acc = 0;
            for (var i = 0; i < count; i++)
            {
                acc += ResolveWeight(slices[i]);
                if (roll < acc)
                    return i;
            }

            return count - 1;
        }

        static int ResolveWeight(WheelSliceDef slice) =>
            slice == null || slice.weight <= 0 ? 1 : slice.weight;
    }
}
