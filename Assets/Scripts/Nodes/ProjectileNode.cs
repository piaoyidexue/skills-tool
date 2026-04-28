using System.Collections;
using UnityEngine;

/// <summary>
///     投射物发射节点 —— 生成投射物，沿指定方向飞行，命中后自动写 Blackboard。
///     支持直线 / 追踪 / 抛物线三种弹道。
///     【对象池】优先从 VFX 对象池获取预制体实例。
/// </summary>
public class ProjectileNode : SkillNode
{
    public enum LaunchMode { TowardTarget, CasterForward, CustomDirection }
    public enum HomingMode { None, TrackTarget, TrackNearest }

    public LaunchMode launchMode = LaunchMode.TowardTarget;
    public HomingMode homing = HomingMode.None;
    public Vector3 customDirection = Vector3.forward;

    public FloatBinding projectileSpeed = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.ProjectileSpeed
    };

    public StringBinding projectilePrefab = new()
    {
        Source = StringBinding.SourceType.SkillConfigField,
        SkillConfigFieldName = nameof(SkillConfig.ProjectilePrefab)
    };

    public FloatBinding damage = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.Damage
    };

    public StringBinding impactVfxKey = new()
    {
        Source = StringBinding.SourceType.SkillConfigField,
        SkillConfigFieldName = nameof(SkillConfig.ImpactVFXKey)
    };

    public float lifetime = 5f;
    public bool waitForCompletion = true;

    [System.NonSerialized] private Projectile _projectile;
    [System.NonSerialized] private bool _launched;

    public override void OnEnter(SkillContext ctx)
    {
        _launched = false;
        _projectile = null;
    }

    public override NodeTickResult Tick(SkillContext ctx, float deltaTime)
    {
        var speed = projectileSpeed.Resolve(ctx);
        if (speed <= 0f)
        {
            Debug.LogWarning("[ProjectileNode] ProjectileSpeed is 0, cannot launch.");
            return NodeTickResult.Success;
        }

        if (!_launched)
        {
            // 首次：获取并启动投射物
            _projectile = GetProjectile(ctx);
            if (_projectile == null)
            {
                Debug.LogError("[ProjectileNode] Failed to get projectile instance.");
                return NodeTickResult.Success;
            }

            var launchPos = ctx.Caster != null ? ctx.Caster.position : Vector3.zero;
            var launchDir = ResolveDirection(ctx);

            var request = new VFXRequest
            {
                VFXKey = "Projectile_Auto",
                Position = launchPos,
                Direction = launchDir,
                Length = speed,
                Intensity = damage.Resolve(ctx),
                ScaleMultiplier = 1f,
                StyleKey = ctx.Config?.VFXProfileKey,
                Parent = homing != HomingMode.None ? ctx.Target : null
            };

            if (homing == HomingMode.TrackTarget && ctx.Target != null)
                _projectile.SetTarget(ctx.Target);

            _projectile.impactVfxKey = impactVfxKey.Resolve(ctx);
            _projectile.critChance = ctx.Config?.CritChance ?? 0f;
            _projectile.lifetime = lifetime;

            _projectile.Launch(request, null);
            _launched = true;
            ctx.Blackboard.SetValue(BBKey.ProjectileActive, true);
        }

        // 等待完成
        if (waitForCompletion)
        {
            if (_projectile != null && _projectile.HasHit)
            {
                ctx.Blackboard.SetValue(BBKey.ProjectileHitPosition, _projectile.HitPosition);
                if (_projectile.HitTarget != null)
                    ctx.Blackboard.SetValue(BBKey.ProjectileHitTarget, _projectile.HitTarget.gameObject.name);

                ctx.Blackboard.SetValue(BBKey.ProjectileActive, false);
                return NodeTickResult.Success;
            }

            // 持续更新追踪目标
            if (homing == HomingMode.TrackNearest && _projectile != null)
            {
                var nearest = FindNearestEnemy(ctx);
                if (nearest != null) _projectile.SetTarget(nearest);
            }

            return NodeTickResult.Running;
        }

        ctx.Blackboard.SetValue(BBKey.ProjectileActive, false);
        return NodeTickResult.Success;
    }

    public override void OnExit(SkillContext ctx)
    {
        ctx.Blackboard.SetValue(BBKey.ProjectileActive, false);
    }

    public override IEnumerator Execute(SkillContext ctx)
    {
        var speed = projectileSpeed.Resolve(ctx);
        if (speed <= 0f)
        {
            Debug.LogWarning("[ProjectileNode] ProjectileSpeed is 0, cannot launch.");
            yield break;
        }

        var projectile = GetProjectile(ctx);
        if (projectile == null)
        {
            Debug.LogError("[ProjectileNode] Failed to get projectile instance.");
            yield break;
        }

        var launchPos = ctx.Caster != null ? ctx.Caster.position : Vector3.zero;
        var launchDir = ResolveDirection(ctx);

        var request = new VFXRequest
        {
            VFXKey = "Projectile_Auto",
            Position = launchPos,
            Direction = launchDir,
            Length = speed,
            Intensity = damage.Resolve(ctx),
            ScaleMultiplier = 1f,
            StyleKey = ctx.Config?.VFXProfileKey,
            Parent = homing != HomingMode.None ? ctx.Target : null
        };

        if (homing == HomingMode.TrackTarget && ctx.Target != null)
            projectile.SetTarget(ctx.Target);

        projectile.impactVfxKey = impactVfxKey.Resolve(ctx);
        projectile.critChance = ctx.Config?.CritChance ?? 0f;
        projectile.lifetime = lifetime;

        var completed = false;
        projectile.Launch(request, _ => { completed = true; });

        ctx.Blackboard.SetValue(BBKey.ProjectileActive, true);

        if (waitForCompletion)
        {
            while (!completed && !ctx.IsInterrupted)
            {
                if (homing == HomingMode.TrackNearest)
                {
                    var nearest = FindNearestEnemy(ctx);
                    if (nearest != null) projectile.SetTarget(nearest);
                }

                yield return null;
            }

            if (projectile.HasHit)
            {
                ctx.Blackboard.SetValue(BBKey.ProjectileHitPosition, projectile.HitPosition);
                if (projectile.HitTarget != null)
                    ctx.Blackboard.SetValue(BBKey.ProjectileHitTarget, projectile.HitTarget.gameObject.name);
            }
        }

        ctx.Blackboard.SetValue(BBKey.ProjectileActive, false);
    }

    private Vector3 ResolveDirection(SkillContext ctx)
    {
        return launchMode switch
        {
            LaunchMode.TowardTarget => ctx.Target != null
                ? (ctx.Target.position - (ctx.Caster != null ? ctx.Caster.position : Vector3.zero)).normalized
                : Vector3.forward,
            LaunchMode.CasterForward => ctx.Caster != null ? ctx.Caster.forward : Vector3.forward,
            LaunchMode.CustomDirection => customDirection.normalized,
            _ => Vector3.forward
        };
    }

    private static Projectile GetProjectile(SkillContext ctx)
    {
        var prefabKey = ctx.Config?.ProjectilePrefab;
        if (!string.IsNullOrWhiteSpace(prefabKey))
        {
            var prefabObj = Resources.Load<GameObject>($"VFX/Prefabs/{prefabKey}");
            if (prefabObj != null)
            {
                var go = Object.Instantiate(prefabObj);
                return go.GetComponent<Projectile>() ?? go.AddComponent<Projectile>();
            }
        }

        var fallback = new GameObject("Projectile_Dynamic");
        fallback.hideFlags = HideFlags.HideAndDontSave;
        return fallback.AddComponent<Projectile>();
    }

    private Transform FindNearestEnemy(SkillContext ctx)
    {
        if (ctx.Target != null) return ctx.Target;

        var colliders = Physics.OverlapSphere(
            ctx.Caster != null ? ctx.Caster.position : Vector3.zero, 20f);

        Transform nearest = null;
        var nearestDist = float.MaxValue;

        foreach (var col in colliders)
        {
            if (col.transform == ctx.Caster) continue;
            var dmg = col.GetComponent<IDamageable>();
            if (dmg == null) continue;
            var dist = Vector3.Distance(
                ctx.Caster != null ? ctx.Caster.position : Vector3.zero, col.transform.position);
            if (dist < nearestDist) { nearestDist = dist; nearest = col.transform; }
        }

        return nearest;
    }
}
