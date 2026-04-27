using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BeamVFX : VFXBase
{
    [SerializeField] private float length = 5f;
    [SerializeField] private float width = 0.3f;
    [SerializeField] private Color startColor = new(0.8f, 0.95f, 1f, 0.85f);
    [SerializeField] private Color endColor = new(0.35f, 0.75f, 1f, 0.1f);

    private LineRenderer _line;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.enabled = false;
        _line.widthMultiplier = width;
        _line.positionCount = 2;
    }

    public override void Play(VFXRequest request)
    {
        transform.SetParent(request.Parent);
        transform.position = request.Position;
        var direction = request.Direction.sqrMagnitude > 0f ? request.Direction.normalized : Vector3.forward;
        var beamLength = request.Length > 0f ? request.Length : length;
        var beamWidth = width * Mathf.Max(0.1f, request.WidthMultiplier) * Mathf.Max(0.1f, request.ScaleMultiplier);
        var primary = request.PrimaryColor == default ? startColor : request.PrimaryColor;
        var accent = request.AccentColor == default ? endColor : request.AccentColor;
        _line.enabled = true;
        _line.widthMultiplier = beamWidth;
        _line.startColor = primary;
        _line.endColor = accent;
        _line.SetPosition(0, request.Position);
        _line.SetPosition(1, request.Position + direction * beamLength);
    }

    public override void Stop()
    {
        if (_line != null) _line.enabled = false;
    }
}
