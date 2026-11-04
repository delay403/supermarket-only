using System.Collections.Generic;
using UnityEngine;

namespace Game.Leaderboard
{
    /// <summary>
    /// 虚拟玩家库：拖入多条 <see cref="VirtualPlayerDef"/>。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Leaderboard/Virtual Player Database", fileName = "VirtualPlayerDatabase")]
    public sealed class VirtualPlayerDatabase : ScriptableObject
    {
        public List<VirtualPlayerDef> players = new List<VirtualPlayerDef>();

        /// <summary>解析每条 Def 的稳定 Id（生成器应写入 virtualId；否则退回资源名）。</summary>
        public static string ResolveVirtualId(VirtualPlayerDef def)
        {
            if (def == null)
                return "";

            if (!string.IsNullOrWhiteSpace(def.virtualId))
                return def.virtualId.Trim();

            return def.name;
        }
    }
}
