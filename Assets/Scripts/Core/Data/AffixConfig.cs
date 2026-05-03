using System;

/// <summary>
///     词缀配置数据结构。
///     对应 Affix.csv 的每一行数据。
/// </summary>
[Serializable]
public class AffixConfig
{
    /// <summary>词缀唯一ID</summary>
    public int AffixID;

    /// <summary>词缀名称</summary>
    public string Name;

    /// <summary>授予的GameplayEffect ID</summary>
    public int GrantedGEID;

    /// <summary>VFX特效Key</summary>
    public string VFXKey;

    /// <summary>颜色色调（十六进制）</summary>
    public string ColorTint;
}