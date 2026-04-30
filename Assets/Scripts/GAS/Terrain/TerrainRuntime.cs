using UnityEngine;

public class TerrainRuntime
{
    public string TerrainTag;
    public Vector3 Position;
    public float Radius;
    public float Duration;
    public float Remaining;

    public bool IsActive => Remaining > 0f;
}
