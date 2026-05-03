using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     词缀应用器 —— 负责为怪物添加词缀效果。
///     基于 Affix.csv 配置，注入 GameplayEffect、材质Tint和VFX特效。
/// </summary>
public static class AffixApplier
{
    /// <summary>
    ///     为怪物应用词缀。
    /// </summary>
    /// <param name="monsterTransform">怪物Transform</param>
    /// <param name="affixId">词缀ID</param>
    public static void ApplyAffix(Transform monsterTransform, int affixId)
    {
        if (monsterTransform == null)
        {
            Debug.LogError("[AffixApplier] monsterTransform is null");
            return;
        }

        var config = ConfigLoader.GetAffixConfig(affixId);
        if (config == null)
        {
            Debug.LogError($"[AffixApplier] Affix config not found: {affixId}");
            return;
        }

        // 应用GameplayEffect
        ApplyGameplayEffect(monsterTransform, config.GrantedGEID);

        // 应用材质Tint
        ApplyMaterialTint(monsterTransform, config.ColorTint);

        // 应用VFX特效
        ApplyVFX(monsterTransform, config.VFXKey);
    }

    /// <summary>
    ///     批量应用多个词缀。
    /// </summary>
    /// <param name="monsterTransform">怪物Transform</param>
    /// <param name="affixIds">词缀ID数组</param>
    public static void ApplyAffixes(Transform monsterTransform, int[] affixIds)
    {
        foreach (var affixId in affixIds)
        {
            ApplyAffix(monsterTransform, affixId);
        }
    }

    /// <summary>
    ///     应用GameplayEffect。
    /// </summary>
    private static void ApplyGameplayEffect(Transform monsterTransform, int geId)
    {
        var geHost = monsterTransform.GetComponent<GEHost>();
        if (geHost == null)
        {
            Debug.LogWarning($"[AffixApplier] GEHost not found on {monsterTransform.name}, skipping GE application.");
            return;
        }

        var geData = ConfigLoader.GetGameplayEffectData(geId);
        if (geData == null)
        {
            Debug.LogError($"[AffixApplier] GameplayEffect data not found: {geId}");
            return;
        }

        // 创建GE配置并应用
        var geConfig = new GEConfig
        {
            GEId = geData.EffectId,
            Name = geData.EffectName,
            DurationPolicy = geData.DurationPolicy,
            Duration = geData.Duration,
            Period = geData.Period,
            StackPolicy = geData.StackPolicy,
            MaxStacks = geData.MaxStacks
        };

        // TODO: 【架构规范】GE modifiers 应由 EffectSystem 统一注入，此处暂不支持
        // 当前 GameplayEffectData 不含 Modifiers 字段，需在 EffectSystem.ApplyEffect() 中统一处理 BaseDamage/Healing
        // geConfig.Modifiers.Add(...);

        // 添加GrantedTags
        geConfig.GrantedTags.AddRange(geData.GrantedTags);

        // 应用效果（内部接口，仅限 EffectSystem 使用）
        // 【警告】直接调用 ApplyEffectInternal 违反架构红线，后续必须迁移到 EffectSystem.ApplyEffect()
        geHost.ApplyEffectInternal(geConfig, monsterTransform);
    }

    /// <summary>
    ///     应用材质Tint。
    /// </summary>
    private static void ApplyMaterialTint(Transform monsterTransform, string colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return;

        // 尝试获取渲染器组件
        var renderer = monsterTransform.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = monsterTransform.GetComponentInChildren<Renderer>();
        }

        if (renderer != null && renderer.material != null)
        {
            try
            {
                // 解析十六进制颜色
                if (ColorUtility.TryParseHtmlString(colorHex, out Color color))
                {
                    renderer.material.SetColor("_Color", color);
                    renderer.material.SetColor("_EmissionColor", color * 0.5f);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AffixApplier] Failed to apply material tint: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     应用VFX特效。
    /// </summary>
    private static void ApplyVFX(Transform monsterTransform, string vfxKey)
    {
        if (string.IsNullOrEmpty(vfxKey)) return;

        // 查找VFX预制体
        var vfxPrefab = Resources.Load<GameObject>($"VFX/{vfxKey}");
        if (vfxPrefab == null)
        {
            Debug.LogWarning($"[AffixApplier] VFX prefab not found: {vfxKey}");
            return;
        }

        // 实例化VFX
        var vfxInstance = GameObject.Instantiate(vfxPrefab, monsterTransform.position, Quaternion.identity, monsterTransform);
        
        // 设置父级关系
        vfxInstance.transform.SetParent(monsterTransform, false);
        
        // 设置偏移（脚底位置）
        vfxInstance.transform.localPosition = Vector3.zero;
        vfxInstance.transform.localRotation = Quaternion.identity;
        
        // 启动VFX
        var vfxSystem = vfxInstance.GetComponent<ParticleSystem>();
        if (vfxSystem != null)
        {
            vfxSystem.Play();
        }
    }
}