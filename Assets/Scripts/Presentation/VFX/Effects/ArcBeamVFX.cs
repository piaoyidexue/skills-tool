using UnityEngine;

/// <summary>
///     细长多段跳链光束 —— 模拟电弧在多目标间跳跃的锯齿链路。
///     使用多段 LineRenderer 在起点与终点之间生成 zigzag 中间点。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ArcBeamVFX : VFXBase
{
    [SerializeField] private float length = 6f;
    [SerializeField] private float width = 0.12f;
    [SerializeField] private int segmentCount = 5;
    [SerializeField] private float jitterAmount = 0.35f;
    [SerializeField] private Color startColor = new(0.55f, 0.82f, 1f, 0.9f);
    [SerializeField] private Color endColor = new(0.35f, 0.6f, 1f, 0.05f);

    private LineRenderer _line;
    private Vector3[] _positions;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.enabled = false;
        _line.textureMode = LineTextureMode.Tile;
        _line.alignment = LineAlignment.View;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.numCapVertices = 2;
        _line.numCornerVertices = 2;
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

        var segs = Mathf.Max(2, segmentCount);
        _line.positionCount = segs + 1;
        _line.widthMultiplier = beamWidth;
        _line.startColor = primary;
        _line.endColor = accent;

        if (_positions == null || _positions.Length != segs + 1)
            _positions = new Vector3[segs + 1];

        var step = beamLength / segs;
        var right = Vector3.Cross(direction, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.01f) right = Vector3.Cross(direction, Vector3.forward).normalized;
        var up = Vector3.Cross(direction, right).normalized;

        for (var i = 0; i <= segs; i++)
        {
            var t = i / (float)segs;
            var pos = request.Position + direction * (t * beamLength);

            // zigzag: alternates direction each segment
            if (i > 0 && i < segs)
            {
                var jitter = Mathf.Sin(t * Mathf.PI * segs * 1.7f) * jitterAmount * beamWidth * 6f;
                var lateralJitter = Mathf.Cos(t * Mathf.PI * segs * 0.9f + 1.3f) * jitterAmount * beamWidth * 3f;
                pos += right * jitter + up * lateralJitter;
            }

            _positions[i] = pos;
        }

        _line.SetPositions(_positions);
        _line.enabled = true;
    }

    public override void Stop()
    {
        if (_line != null) _line.enabled = false;
    }
}
