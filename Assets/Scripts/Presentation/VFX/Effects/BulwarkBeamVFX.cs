using UnityEngine;

/// <summary>
///     宽厚墙体光束 —— 厚重墙体般的宽光束，使用双层 LineRenderer 表现墙体的厚度与稳定性。
///     主束宽厚，辅束在两侧形成墙体边缘。
/// </summary>
public class BulwarkBeamVFX : VFXBase
{
    [SerializeField] private float length = 5f;
    [SerializeField] private float width = 0.55f;
    [SerializeField] private float wallThickness = 0.25f;
    [SerializeField] private int rippleSegments = 6;
    [SerializeField] private Color wallColor = new(0.9f, 0.78f, 0.35f, 0.8f);
    [SerializeField] private Color coreColor = new(1f, 0.95f, 0.65f, 0.9f);

    private LineRenderer _mainLine;
    private LineRenderer _edgeLineA;
    private LineRenderer _edgeLineB;
    private bool _initialised;

    private void EnsureComponents()
    {
        if (_initialised) return;

        _mainLine = GetComponent<LineRenderer>();
        if (_mainLine == null) _mainLine = gameObject.AddComponent<LineRenderer>();

        var edgeAObj = new GameObject("BulwarkEdgeA");
        edgeAObj.transform.SetParent(transform);
        _edgeLineA = edgeAObj.AddComponent<LineRenderer>();
        ConfigureLine(_edgeLineA, 0.05f);

        var edgeBObj = new GameObject("BulwarkEdgeB");
        edgeBObj.transform.SetParent(transform);
        _edgeLineB = edgeBObj.AddComponent<LineRenderer>();
        ConfigureLine(_edgeLineB, 0.05f);

        ConfigureLine(_mainLine, 1f);
        _initialised = true;
    }

    private static void ConfigureLine(LineRenderer line, float widthMult)
    {
        line.textureMode = LineTextureMode.Tile;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.positionCount = 2;
        line.widthMultiplier = widthMult;
        line.enabled = false;
    }

    public override void Play(VFXRequest request)
    {
        EnsureComponents();

        transform.SetParent(request.Parent);
        transform.position = request.Position;

        var direction = request.Direction.sqrMagnitude > 0f ? request.Direction.normalized : Vector3.forward;
        var beamLength = request.Length > 0f ? request.Length : length;
        var beamWidth = width * Mathf.Max(0.1f, request.WidthMultiplier) * Mathf.Max(0.1f, request.ScaleMultiplier);
        var primary = request.PrimaryColor == default ? wallColor : request.PrimaryColor;
        var accent = request.AccentColor == default ? coreColor : request.AccentColor;

        var right = Vector3.Cross(direction, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.01f) right = Vector3.Cross(direction, Vector3.forward).normalized;
        var up = Vector3.Cross(direction, right).normalized;

        var endPos = request.Position + direction * beamLength;
        var offset = up * wallThickness * beamWidth;

        // main wide wall beam
        _mainLine.widthMultiplier = beamWidth;
        _mainLine.startColor = primary;
        _mainLine.endColor = new Color(primary.r, primary.g, primary.b, 0.05f);
        _mainLine.SetPosition(0, request.Position);
        _mainLine.SetPosition(1, endPos);
        _mainLine.enabled = true;

        // edge lines for wall definition
        _edgeLineA.widthMultiplier = beamWidth * 0.12f;
        _edgeLineA.startColor = accent;
        _edgeLineA.endColor = new Color(accent.r, accent.g, accent.b, 0.02f);
        _edgeLineA.SetPosition(0, request.Position + offset);
        _edgeLineA.SetPosition(1, endPos + offset);
        _edgeLineA.enabled = true;

        _edgeLineB.widthMultiplier = beamWidth * 0.12f;
        _edgeLineB.startColor = accent;
        _edgeLineB.endColor = new Color(accent.r, accent.g, accent.b, 0.02f);
        _edgeLineB.SetPosition(0, request.Position - offset);
        _edgeLineB.SetPosition(1, endPos - offset);
        _edgeLineB.enabled = true;
    }

    public override void Stop()
    {
        if (_mainLine != null) _mainLine.enabled = false;
        if (_edgeLineA != null) _edgeLineA.enabled = false;
        if (_edgeLineB != null) _edgeLineB.enabled = false;
    }
}
