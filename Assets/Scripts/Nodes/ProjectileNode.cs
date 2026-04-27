using System.Collections;
using UnityEngine;

/// <summary>
///     投射物发射节点 —— 生成投射物，沿指定方向飞行，命中后自动写 Blackboard。
///     支持直线 / 追踪 / 抛物线三种弹道。
///     【对象池】优先从 VFX 对象池获取预制体实例。
/// </summary>
public class ProjectileNode : SkillNode
{
    public enum LaunchMode
    {
        /// <summary>朝向目标直线发射</summary>
        TowardTarget,

        /// <summary>施法者前方发射</summary>
        CasterForward,

        /// <summary>自定义方向</summary>
        CustomDirection
    }

    public enum HomingMode
    {
        /// <summary>不追踪</summary>
        None,

        /// <summary>追踪初始目标</summary>
        TrackTarget,

        /// <summary>追踪最近敌人</summary>
        TrackNearest
    }

    /// <summary>发射模式</summary>
    public LaunchMode launchMode = LaunchMode.TowardTarget;

    /// <summary>追踪模式</summary>
    public HomingMode homing = HomingMode.None;

    /// <summary>自定义方向（仅 CustomDirection 时生效）</summary>
    public Vector3 customDirection = Vector3.forward;

    /// <summary>投射物飞行速度，留空从 SkillConfig.ProjectileSpeed 读取</summary>
    public FloatBinding projectileSpeed = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.ProjectileSpeed
    };

    /// <summary>投射物预制体 Key</summary>
    public StringBinding projectilePrefab = new()
    {
        Source = StringBinding.SourceType.SkillConfigField,
        SkillConfigFieldName = nameof(SkillConfig.ProjectilePrefab)
    };

    /// <summary>伤害值，留空用 SkillConfig.Damage</summary>
    public FloatBinding damage = new()
    {
        Source = FloatBinding.SourceType.SkillConfig,
        SkillField = SkillFloatField.Damage
    };

    /// <summary>命中特效 Key</summary>
    public StringBinding impactVfxKey = new()
    {
        Source = StringBinding.SourceType.SkillConfigField,
        SkillConfigFieldName = nameof(SkillConfig.ImpactVFXKey)
    };

    /// <summary>最大存活时间</summary>
    public float lifetime = 5f;

    /// <summary>是否等待投射物完成后再继续后继节点</summary>
    public bool waitForCompletion = true;

    public override IEnumerator Execute(SkillContext ctx)
    {
        var speed = projectileSpeed.Resolve(ctx);
        if (speed <= 0f)
        {
            Debug.LogWarning("[ProjectileNode] ProjectileSpeed is 0, cannot launch.");
            yield break;
        }

        // 获取或创建投射物实例
        var projectile = GetProjectile(ctx);
        if (projectile == null)
        {
            Debug.LogError("[ProjectileNode] Failed to get projectile instance.");
            yield break;
        }

        // 解析发射信息
        var launchPos = ctx.Caster != null ? ctx.Caster.position : Vector3.zero;
        var launchDir = ResolveDirection(ctx);

        // 启动投射物
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

        // 设置追踪目标
        if (homing == HomingMode.TrackTarget && ctx.Target != null)
        {
            projectile.SetTarget(ctx.Target);
        }

        projectile.impactVfxKey = impactVfxKey.Resolve(ctx);
        projectile.critChance = ctx.Config?.CritChance ?? 0f;
        projectile.lifetime = lifetime;

        var completed = false;
        projectile.Launch(request, _ => { completed = true; });

        ctx.Blackboard.SetValue(BBKey.ProjectileActive, true);

        // 等待完成或立即继续
        if (waitForCompletion)
        {
            while (!completed && !ctx.IsInterrupted)
            {
                // 持续更新追踪目标位置（追踪最近敌人）
                if (homing == HomingMode.TrackNearest)
                {
                    var nearest = FindNearestEnemy(ctx);
                    if (nearest != null) projectile.SetTarget(nearest);
                }

                yield return null;
            }

            // 命中后写入 Blackboard
            if (projectile.HasHit)
            {
                ctx.Blackboard.SetValue(BBKey.ProjectileHitPosition, projectile.HitPosition);
                if (projectile.HitTarget != null)
                {
                    ctx.Blackboard.SetValue(BBKey.ProjectileHitTarget,
                        projectile.HitTarget.gameObject.name);
                }
            }
        }

        ctx.Blackboard.SetValue(BBKey.ProjectileActive, false);
    }

    public override SkillNode ResolveNextNode(SkillContext ctx)
    {
        if (ctx.IsInterrupted) return null;
        return base.ResolveNextNode(ctx);
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
        // 从 SkillConfig 读取预制体 Key
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

        // fallback: 动态创建
        var fallback = new GameObject("Projectile_Dynamic");
        fallback.hideFlags = HideFlags.HideAndDontSave;
        return fallback.AddComponent<Projectile>();
    }

    private Transform FindNearestEnemy(SkillContext ctx)
    {
        if (ctx.Target != null) return ctx.Target;

        var colliders = Physics.OverlapSphere(
            ctx.Caster != null ? ctx.Caster.position : Vector3.zero,
            20f);

        Transform nearest = null;
        var nearestDist = float.MaxValue;

        foreach (var col in colliders)
        {
            if (col.transform == ctx.Caster) continue;

            var dmg = col.GetComponent<IDamageable>();
            if (dmg == null) continue;

            var dist = Vector3.Distance(
                ctx.Caster != null ? ctx.Caster.position : Vector3.zero,
                col.transform.position);

            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = col.transform;
            }
        }

        return nearest;
    }
}
