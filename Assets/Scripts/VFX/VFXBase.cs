using UnityEngine;

public abstract class VFXBase : MonoBehaviour
{
    public abstract void Play(VFXRequest request);
    public abstract void Stop();
}