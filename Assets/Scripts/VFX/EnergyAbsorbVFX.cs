using UnityEngine;

/// <summary>
///     吸能特效 —— 终结技第一阶段：能量从周围向中心塌缩吸入。
///     使用 Quad + EnergyAbsorb Shader，从大范围向内收缩到核心点。
///     收缩完成后自动 Stop，为第二阶段爆发做准备。
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class EnergyAbsorbVFX : VFXBase
{
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private float startScale = 3.5f;
    [SerializeField] private float endScale = 0.2f;
    [SerializeField] private Color tintColor = new(0.55f, 0.3f, 1f, 1f);
    [SerializeField] private Color coreGlow = new(1f, 1f, 1f, 1f);

    private float _elapsed;
    private bool _playing;
    private MaterialPropertyBlock _propertyBlock;
    private float _runtimeDuration;
    private float _runtimeStartScale;
    private float _runtimeEndScale;
    private Color _runtimeTint;
    private Color _runtimeCore;

    private MeshRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _propertyBlock = new MaterialPropertyBlock();
        EnsureQuadMesh();
        Stop();
    }

    private void Update()
    {
        if (!_playing) return;

        _elapsed += Time.deltaTime;
        var progress = _runtimeDuration > 0f ? Mathf.Clamp01(_elapsed / _runtimeDuration) : 1f;

        // ease-in: slow start, accelerates as it collapses
        var easedProgress = progress < 0.4f
            ? progress * progress * 1.5f
            : 1f - Mathf.Pow(1f - (progress - 0.4f) / 0.6f, 3f);

        var scale = Mathf.Lerp(_runtimeStartScale, _runtimeEndScale, easedProgress);
        transform.localScale = new Vector3(scale, scale, 1f);
        if (Camera.main != null) transform.forward = Camera.main.transform.forward;

        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor("_Tint", _runtimeTint);
        _propertyBlock.SetColor("_CoreGlow", _runtimeCore);
        _propertyBlock.SetFloat("_Progress", easedProgress);
        _renderer.SetPropertyBlock(_propertyBlock);

        if (progress >= 1f) Stop();
    }

    public override void Play(VFXRequest request)
    {
        transform.SetParent(request.Parent);
        transform.position = request.Position;

        var intensity = Mathf.Max(0.1f, request.Intensity);
        _runtimeDuration = request.Duration > 0f ? request.Duration : duration;
        _runtimeStartScale = startScale * Mathf.Max(0.1f, request.ScaleMultiplier);
        _runtimeEndScale = endScale;
        _runtimeTint = request.PrimaryColor == default ? tintColor : request.PrimaryColor;
        _runtimeTint.a *= Mathf.Clamp01(intensity);
        _runtimeCore = request.AccentColor == default ? coreGlow : request.AccentColor;

        _elapsed = 0f;
        _playing = true;
        _renderer.enabled = true;
        transform.localScale = Vector3.one * _runtimeStartScale;
    }

    public override void Stop()
    {
        _playing = false;
        if (_renderer != null) _renderer.enabled = false;
    }

    private void EnsureQuadMesh()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter.sharedMesh != null) return;

        var mesh = new Mesh { name = "ElementLine_EnergyAbsorbQuad" };
        mesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        });
        mesh.SetUVs(0, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        });
        mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
    }
}
