using System;
using System.Collections.Generic;

/// <summary>
///     阵容小队配置数据结构。
///     对应 Squad.csv 的每一行数据。
/// </summary>
[Serializable]
public class SquadConfig
{
    /// <summary>小队唯一ID</summary>
    public int SquadID;

    /// <summary>小队名称</summary>
    public string Name;

    /// <summary>成员数量</summary>
    public int MemberCount;

    /// <summary>怪物ID列表（管道符分隔）</summary>
    public string MonsterIDList;

    /// <summary>解析后的怪物ID数组</summary>
    public List<int> MonsterIDs = new List<int>();
}