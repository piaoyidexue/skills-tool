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
    ///     应用GameplayEffect（重构版：完全符合 GAS 架构规范）
    /// </summary>
    private static void ApplyGameplayEffect(Transform monsterTransform, int geId)
    {
        // 1. 校验目标是否具备受击/受效能力
        var geHost = monsterTransform.GetComponent<GEHost>();
        if (geHost == null)
        {
            Debug.LogWarning($"[AffixApplier] GEHost not found on {monsterTransform.name}, skipping GE application.");
            return;
        }

        // 2. 从配置中心获取静态数据
        var geData = ConfigLoader.GetGameplayEffectData(geId);
        if (geData == null)
        {
            Debug.LogError($"[AffixApplier] GameplayEffect data not found: {geId}");
            return;
        }

        // 3. 组装标准结算上下文
        // 词缀通常由环境或系统施加，因此 Instigator 可以填 null，或者填 monsterTransform (Self-Apply)
        var context = new EffectContext
        {
            Target = monsterTransform,
            TargetHost = geHost,
            TargetPoint = monsterTransform.position,
            Instigator = null,       // 来源：系统
            InstigatorHost = null    // 来源没有 GEHost
            // Level = 1             // 如果词缀有等级，可在此处传入
        };

        // 4. 【核心】通过 EffectSystem 统一管线派发
        // 内部会自动经过：免疫Tag校验 -> Modifier组装 -> 反应处理 -> ApplyBuffEffect -> 事件广播
        EffectSystem.ApplyEffect(context, geData);
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