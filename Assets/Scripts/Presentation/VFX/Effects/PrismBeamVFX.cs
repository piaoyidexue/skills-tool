using UnityEngine;

/// <summary>
///     折线棱镜光束 —— 分段折角折射束，在每个棱面交界处产生明显的角度偏移。
///     使用 LineRenderer 在光束路径上插入棱镜折点。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PrismBeamVFX : VFXBase
{
    [SerializeField] private float length = 5.5f;
    [SerializeField] private float width = 0.22f;
    [SerializeField] private int facetCount = 4;
    [SerializeField] private float bendAngle = 12f;
    [SerializeField] private Color startColor = new(0.75f, 0.88f, 1f, 0.9f);
    [SerializeField] private Color endColor = new(0.4f, 0.72f, 0.95f, 0.08f);

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
        _line.numCapVertices = 4;
        _line.numCornerVertices = 4;
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

        var facets = Mathf.Max(2, facetCount);
        _line.positionCount = facets + 1;
        _line.widthMultiplier = beamWidth;
        _line.startColor = primary;
        _line.endColor = accent;

        if (_positions == null || _positions.Length != facets + 1)
            _positions = new Vector3[facets + 1];

        var step = beamLength / facets;
        var up = Vector3.Cross(direction, Vector3.right).normalized;
        if (up.sqrMagnitude < 0.01f) up = Vector3.up;

        _positions[0] = request.Position;

        for (var i = 1; i <= facets; i++)
        {
            // each facet bends the beam slightly in an alternating direction
            var sign = (i % 2 == 0) ? 1f : -1f;
            var bendAxis = Vector3.Cross(direction, up).normalized;
            var bentDir = Quaternion.AngleAxis(bendAngle * sign, bendAxis) * direction;

            _positions[i] = _positions[i - 1] + bentDir * step;

            // restore direction for next segment (alternate folds)
            if (i < facets)
                direction = bentDir;
        }

        _line.SetPositions(_positions);
        _line.enabled = true;
    }

    public override void Stop()
    {
        if (_line != null) _line.enabled = false;
    }
}
