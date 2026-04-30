using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// EQS 查询可视化器
/// - 扇形/线条/准星绘制
/// - 符合条件 = 蓝色准星，不符合 = 红叉
/// - Job System Profiler Marker 性能统计
/// </summary>
[AddComponentMenu("Skills QA/EQS Debugger")]
public class QAEQSDebugger : MonoBehaviour
{
    [Header("查询配置")]
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _fieldOfView = 60f;
    [SerializeField] private int _targetTeamId = 2; // 默认敌人团队

    [Header("可视化配置")]
    [SerializeField] private Color _fanColor = new Color(0.3f, 1f, 0.5f, 0.25f);
    [SerializeField] private Color _hitColor = new Color(0.2f, 0.6f, 1f, 0.9f);
    [SerializeField] private Color _missColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private float _lineWidth = 0.05f;

    [Header("性能统计")]
    [SerializeField] private bool _enablePerformanceMetrics = true;

    // 运行结果
    private bool _lastQuerySuccess;
    private SensorQueryResult _lastResult;
    private readonly List<Transform> _hitTargets = new();
    private readonly List<Transform> _missTargets = new();

    // 性能统计
    private float _lastQueryTimeMs;
    private int _lastEntityCount;

    // Gizmo 显示状态
    private bool _showQuery;
    private Vector3 _queryOrigin;
    private Vector3 _queryForward;
    private float _queryRange;
    private float _queryFov;
    private float _queryDisplayTime;

    // ===== 公共 API =====

    /// <summary>执行 EQS 查询</summary>
    public void ExecuteQuery(Vector3 origin, Vector3 forward, float fov, float range, int targetTeamId = -1)
    {
        _queryOrigin = origin;
        _queryForward = forward;
        _queryRange = range;
        _queryFov = fov;
        _showQuery = true;
        _queryDisplayTime = Time.time;

        _hitTargets.Clear();
        _missTargets.Clear();

        var sw = Stopwatch.StartNew();

        // 从 SpatialHashGrid 获取范围内的实体
        var grid = SpatialHashGrid.Instance;
        if (grid == null)
        {
            LogError("SpatialHashGrid 未找到!");
            return;
        }

        var candidates = new List<ISpatialEntity>();
        grid.QueryRange(origin, range, candidates,
            teamFilter: targetTeamId >= 0 ? targetTeamId : _targetTeamId);

        _lastEntityCount = candidates.Count;

        // 计算扇形区域
        var halfFov = fov * 0.5f * Mathf.Deg2Rad;
        var cosHalfFov = Mathf.Cos(halfFov);

        var attackRangeSq = _attackRange * _attackRange;

        _lastResult = new SensorQueryResult
        {
            EnemyCount = 0,
            AllyCount = 0,
            NearestEnemyEntityId = -1,
            NearestEnemyDistance = float.MaxValue,
            HasTargetInAttackRange = false
        };

        foreach (var entity in candidates)
        {
            var pos = entity.Position;
            var dir = (pos - origin);
            dir.y = 0;
            var distSq = dir.sqrMagnitude;
            dir.Normalize();

            // FOV 检测
            var dot = Vector3.Dot(forward, dir);
            if (dot < cosHalfFov) continue;

            var tf = (entity as MonoBehaviour)?.transform;
            if (tf == null) continue;

            if (entity.TeamId == (targetTeamId >= 0 ? targetTeamId : _targetTeamId))
            {
                _lastResult.EnemyCount++;
                _hitTargets.Add(tf);

                if (distSq < _lastResult.NearestEnemyDistance)
                {
                    _lastResult.NearestEnemyDistance = Mathf.Sqrt(distSq);
                    _lastResult.NearestEnemyEntityId = entity.EntityId;
                }

                if (distSq <= attackRangeSq)
                    _lastResult.HasTargetInAttackRange = true;
            }
            else
            {
                _lastResult.AllyCount++;
            }
        }

        sw.Stop();
        _lastQueryTimeMs = (float)sw.Elapsed.TotalMilliseconds;
        _lastQuerySuccess = true;

        Log($"EQS 查询完成: {Time.time:F2}s | 发现 {_hitTargets.Count} 个目标 | 耗时 {_lastQueryTimeMs:F3}ms");
    }

    /// <summary>使用当前配置执行查询</summary>
    public void ExecuteQuery()
    {
        ExecuteQuery(transform.position, transform.forward, _fieldOfView, _detectionRange, _targetTeamId);
    }

    // ===== Gizmo 绘制 =====

    private void OnDrawGizmosSelected()
    {
        if (!_showQuery) return;

        // 显示时间限制（3秒后自动隐藏）
        if (Time.time - _queryDisplayTime > 3f)
        {
            _showQuery = false;
            return;
        }

        DrawQueryFan();
        DrawTargetIndicators();

        // 绘制性能信息
        #if UNITY_EDITOR
        var info = $"EQS: {_lastEntityCount} entities, {_lastQueryTimeMs:F2}ms";
        UnityEditor.Handles.Label(_queryOrigin + Vector3.up * 2.5f, info);
        #endif
    }

    private void DrawQueryFan()
    {
        var segments = 32;
        var halfAngle = _queryFov * 0.5f * Mathf.Deg2Rad;

        // 绘制扇形填充
        Gizmos.color = _fanColor;
        var prevPoint = _queryOrigin + Quaternion.AngleAxis(-_queryFov * 0.5f, Vector3.up) * _queryForward * _queryRange;
        for (var i = 1; i <= segments; i++)
        {
            var angle = Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments);
            var point = _queryOrigin + Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up) * _queryForward * _queryRange;

            Gizmos.DrawLine(_queryOrigin, point);
            Gizmos.DrawLine(prevPoint, point);

            prevPoint = point;
        }

        // 绘制攻击范围圈
        Gizmos.color = new Color(_fanColor.r, _fanColor.g, _fanColor.b, 0.5f);
        DrawCircle(_queryOrigin, _attackRange, 24);

        // 绘制外圈
        Gizmos.color = new Color(_fanColor.r, _fanColor.g, _fanColor.b, 0.3f);
        DrawCircle(_queryOrigin, _queryRange, 32);
    }

    private void DrawTargetIndicators()
    {
        // 命中的目标 - 蓝色准星
        Gizmos.color = _hitColor;
        foreach (var tf in _hitTargets)
        {
            DrawTargetMarker(tf.position, true);

            // 绘制连线
            Gizmos.DrawLine(_queryOrigin, tf.position);
        }

        // 未命中的目标 - 红色叉
        Gizmos.color = _missColor;
        foreach (var tf in _missTargets)
        {
            DrawTargetMarker(tf.position, false);
        }
    }

    private void DrawTargetMarker(Vector3 pos, bool isHit)
    {
        // 准星样式（命中）
        if (isHit)
        {
            var markerSize = 1.2f;
            var top = pos + Vector3.up * markerSize;

            // 十字准星
            Gizmos.DrawLine(pos + Vector3.left * markerSize, pos + Vector3.right * markerSize);
            Gizmos.DrawLine(pos + Vector3.back * markerSize, pos + Vector3.forward * markerSize);

            // 中心圆
            Gizmos.DrawWireSphere(pos, markerSize * 0.3f);

            // 指向线条
            Gizmos.DrawLine(_queryOrigin, pos);
        }
        // 叉样式（未命中）
        else
        {
            var size = 0.8f;
            var offset = Vector3.up * 0.5f;

            // X 形状
            Gizmos.DrawLine(pos + offset + new Vector3(-1, 0, -1) * size, pos + offset + new Vector3(1, 0, 1) * size);
            Gizmos.DrawLine(pos + offset + new Vector3(-1, 0, 1) * size, pos + offset + new Vector3(1, 0, -1) * size);
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        var prevPoint = center + Vector3.forward * radius;
        for (var i = 1; i <= segments; i++)
        {
            var angle = (float)i / segments * Mathf.PI * 2f;
            var point = center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }

    // ===== 性能统计显示 =====

    private void OnGUI()
    {
        if (!_showQuery || Time.time - _queryDisplayTime > 3f) return;

        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        var bgStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft
        };

        // 左上角显示查询结果
        GUILayout.BeginArea(new Rect(10, 10, 280, 120));
        GUI.backgroundColor = new Color(0, 0, 0, 0.6f);
        GUILayout.Box($"<b>EQS Query Results</b>\n" +
                      $"Targets Found: {_hitTargets.Count}\n" +
                      $"Nearest Distance: {_lastResult.NearestEnemyDistance:F1}m\n" +
                      $"In Attack Range: {_lastResult.HasTargetInAttackRange}\n" +
                      $"Query Time: {_lastQueryTimeMs:F3}ms\n" +
                      $"Entities Scanned: {_lastEntityCount}",
            bgStyle);
        GUI.backgroundColor = Color.white;
        GUILayout.EndArea();

        // 绘制命中目标的编号
        var cam = Camera.current;
        if (cam == null) return;

        for (var i = 0; i < _hitTargets.Count; i++)
        {
            var tf = _hitTargets[i];
            if (tf == null) continue;

            var screenPos = cam.WorldToScreenPoint(tf.position + Vector3.up * 2f);
            if (screenPos.z < 0) continue;

            var label = $"#{i + 1}";
            var content = new GUIContent(label);
            var size = labelStyle.CalcSize(content);

            GUI.backgroundColor = _hitColor;
            GUI.Label(new Rect(screenPos.x - size.x * 0.5f, Screen.height - screenPos.y - size.y * 0.5f,
                size.x + 4, size.y + 2), label, labelStyle);
        }
    }

    // ===== 工具方法 =====

    private void Log(string msg)
    {
        UnityEngine.Debug.Log($"<color=cyan><b>[QAEQSDebugger]</b></color> {msg}");
    }

    private void LogError(string msg)
    {
        UnityEngine.Debug.LogError($"<color=red><b>[QAEQSDebugger]</b></color> {msg}");
    }
}