using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ShockwaveRingVFX : VFXBase
{
    [SerializeField] private float duration = 0.65f;
    [SerializeField] private float startRadius = 0.6f;
    [SerializeField] private float endRadius = 2.6f;
    [SerializeField] private Color startColor = new(1f, 0.72f, 0.32f, 0.95f);
    [SerializeField] private Color endColor = new(1f, 0.28f, 0.08f, 0f);
    private float _elapsed;
    private bool _playing;
    private MaterialPropertyBlock _propertyBlock;
    private Color _runtimeEndColor;
    private Color _runtimeStartColor;
    private float _runtimeDuration;
    private float _runtimeEndRadius;
    private float _runtimeStartRadius;

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
        var radius = Mathf.Lerp(_runtimeStartRadius, _runtimeEndRadius, progress);
        transform.localScale = new Vector3(radius, radius, 1f);

        var color = Color.Lerp(_runtimeStartColor, _runtimeEndColor, progress);
        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor("_Tint", color);
        _propertyBlock.SetFloat("_Progress", progress);
        _renderer.SetPropertyBlock(_propertyBlock);

        if (progress >= 1f) Stop();
    }

    public override void Play(VFXRequest request)
    {
        transform.SetParent(request.Parent);
        transform.position = request.Position + new Vector3(0f, 0.05f, 0f);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var intensity = Mathf.Max(0.1f, request.Intensity);
        _runtimeDuration = request.Duration > 0f ? request.Duration : duration;
        _runtimeStartRadius = startRadius * Mathf.Max(0.1f, request.ScaleMultiplier);
        _runtimeEndRadius = endRadius * Mathf.Max(0.1f, request.ScaleMultiplier);
        _runtimeStartColor = request.PrimaryColor == default ? startColor : request.PrimaryColor;
        _runtimeStartColor.a *= Mathf.Clamp01(intensity);
        _runtimeEndColor = request.AccentColor == default ? endColor : VFXPaletteUtility.Soften(request.AccentColor, 0f);
        transform.localScale = Vector3.one * _runtimeStartRadius;
        _elapsed = 0f;
        _playing = true;
        _renderer.enabled = true;
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

        var mesh = new Mesh { name = "ElementLine_GroundQuad" };
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
