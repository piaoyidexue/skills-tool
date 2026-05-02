using UnityEngine;

public class StatusRuntime
{
    /// <summary>状态类型枚举</summary>
    public StatusType Type;
    /// <summary>来源标签（标识状态来源，如技能名、元素类型）</summary>
    public string SourceTag;
    /// <summary>状态数值（语义随类型不同而不同，如 Slow 为减速比例，Burn 为每跳伤害）</summary>
    public float Value;
    /// <summary>总持续时间（秒）</summary>
    public float Duration;
    /// <summary>剩余持续时间（秒）</summary>
    public float Remaining;
    /// <summary>施放者（来源Transform）</summary>
    public Transform Instigator;

    public bool IsActive => Remaining > 0f;

    public void Reset(float value, float duration, string sourceTag, Transform instigator)
    {
        Value = value;
        Duration = duration;
        Remaining = duration;
        SourceTag = sourceTag;
        Instigator = instigator;
    }

    public override string ToString()
    {
        return $"{Type}({Remaining:F1}s)";
    }
}
