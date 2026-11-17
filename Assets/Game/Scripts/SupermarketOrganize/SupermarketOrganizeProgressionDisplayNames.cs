using Game.SupermarketOrganize.Config;

namespace Game.SupermarketOrganize
{
    public static class SupermarketOrganizeProgressionDisplayNames
    {
        public static string GetSkillName(SupermarketSkillId skillId) => skillId switch
        {
            SupermarketSkillId.Scanner => "扫描仪",
            SupermarketSkillId.Magnet => "磁铁",
            SupermarketSkillId.XRay => "X射线",
            SupermarketSkillId.AutoStocker => "自动存货员",
            _ => skillId.ToString(),
        };

        public static string GetSkillDescription(SupermarketSkillId skillId) => skillId switch
        {
            SupermarketSkillId.Scanner => "高亮所有与手持物品同 ID 的地面物品",
            SupermarketSkillId.Magnet => "将范围内最近物品拉入手持位",
            SupermarketSkillId.XRay => "显示当前物品对应货架提示",
            SupermarketSkillId.AutoStocker => "自动将手持物品放到对应货架",
            _ => string.Empty,
        };

        public static string GetAttributeName(SupermarketAttributeId attributeId) => attributeId switch
        {
            SupermarketAttributeId.PickupRange => "拾取范围",
            SupermarketAttributeId.MoveSpeed => "移动速度",
            SupermarketAttributeId.HandleCapacity => "购物车容量",
            _ => attributeId.ToString(),
        };

        public static string GetAttributeDescription(SupermarketAttributeId attributeId) => attributeId switch
        {
            SupermarketAttributeId.PickupRange => "提高准星射线检测距离",
            SupermarketAttributeId.MoveSpeed => "提高基础移动速度",
            SupermarketAttributeId.HandleCapacity => "提高单次可携带物品数量",
            _ => string.Empty,
        };

        public static string FormatSkillEffect(SupermarketSkillId skillId, int level)
        {
            var cd = SupermarketOrganizeProgressionService.GetSkillCooldownSeconds(skillId, level);
            return $"冷却 {cd:0.#} 秒";
        }

        public static string FormatSkillNextEffect(SupermarketSkillId skillId, int level)
        {
            var config = SupermarketOrganizeProgressionConfig.ResolveRuntime();
            return skillId switch
            {
                SupermarketSkillId.Scanner => $"持续时间 {config.scannerHighlightDurationSeconds:0.#} 秒",
                SupermarketSkillId.Magnet => $"作用半径 {config.magnetPullRadius:0.#} 米",
                SupermarketSkillId.XRay => $"提示时间 {config.xRayTipDurationSeconds:0.#} 秒",
                SupermarketSkillId.AutoStocker => "自动上架手提区商品",
                _ => string.Empty,
            };
        }

        public static string FormatSkillNextCooldown(SupermarketSkillId skillId, int level)
        {
            var cd = SupermarketOrganizeProgressionService.GetSkillCooldownSeconds(skillId, level);
            return $"{cd:0.#} 秒";
        }

        public static string FormatAttributeEffect(SupermarketAttributeId attributeId, int level)
        {
            switch (attributeId)
            {
                case SupermarketAttributeId.PickupRange:
                    return $"范围 {SupermarketOrganizeProgressionService.GetPickupRange(level):0}";
                case SupermarketAttributeId.MoveSpeed:
                    return $"移速 +{SupermarketOrganizeProgressionService.GetMoveSpeedBonus(level) * 100f:0}%";
                case SupermarketAttributeId.HandleCapacity:
                    return $"容量 {SupermarketOrganizeProgressionService.GetHandleCapacity(level)}";
                default:
                    return string.Empty;
            }
        }
    }
}
