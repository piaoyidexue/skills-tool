using UnityEngine;

public class StatusRuntime
{
    public StatusType Type;
    public string SourceTag;
    public float Value;
    public float Duration;
    public float Remaining;
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
