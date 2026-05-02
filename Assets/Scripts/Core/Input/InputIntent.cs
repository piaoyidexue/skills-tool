using System;
using UnityEngine;

/// <summary>
/// 语义化输入意图枚举 - 标准化的输入抽象，不依赖具体硬件
/// </summary>
public enum InputIntent
{
    /// <summary>移动向量（2D方向）</summary>
    Move,

    /// <summary>普通攻击</summary>
    Attack,

    /// <summary>释放技能1</summary>
    Skill1,

    /// <summary>释放技能2</summary>
    Skill2,

    /// <summary>交互/拾取</summary>
    Interact,

    /// <summary>打开菜单</summary>
    OpenMenu,

    /// <summary>跳跃</summary>
    Jump,

    /// <summary>闪避</summary>
    Dodge,

    /// <summary>防御</summary>
    Block,

    /// <summary>瞄准</summary>
    Aim
}

/// <summary>
/// 输入意图结构体 - 包含时间戳和额外数据的语义化指令
/// </summary>
public struct InputCommand
{
    public InputIntent Intent;
    public float Timestamp;
    public Vector2 MoveVector;
    public int SkillId;
    public Transform Target;
    
    public InputCommand(InputIntent intent)
    {
        Intent = intent;
        Timestamp = Time.time;
        MoveVector = Vector2.zero;
        SkillId = 0;
        Target = null;
    }
    
    public InputCommand(InputIntent intent, Vector2 moveVector) : this(intent)
    {
        MoveVector = moveVector;
    }
    
    public InputCommand(InputIntent intent, int skillId) : this(intent)
    {
        SkillId = skillId;
    }
    
    public InputCommand(InputIntent intent, Transform target) : this(intent)
    {
        Target = target;
    }
}