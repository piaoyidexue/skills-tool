public enum StatusType
{
    /// <summary>无状态</summary>
    None,
    /// <summary>燃烧：周期性火焰伤害</summary>
    Burn,
    /// <summary>寒冷：降低移动速度，可与 Conductive 触发超导</summary>
    Chill,
    /// <summary>导电：雷元素标记，可与 Fire 触发超载、与 Chill 触发超导</summary>
    Conductive,
    /// <summary>标记：被标记目标受到额外伤害</summary>
    Mark,
    /// <summary>冰冻：完全无法移动和攻击</summary>
    Freeze,
    /// <summary>减速：降低移动速度</summary>
    Slow,
    /// <summary>眩晕：无法行动</summary>
    Stun,
    /// <summary>中毒：周期性毒素伤害</summary>
    Poison,
    /// <summary>定身：无法移动，但可攻击</summary>
    Root
}
