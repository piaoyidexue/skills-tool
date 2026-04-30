using System;
using System.Collections.Generic;
using UnityEngine;

public class VFXManager : MonoBehaviour
{
    [SerializeField] private List<VFXEntry> vfxEntries = new();

    private readonly VFXObjectPool _pool = new();
    private readonly HashSet<string> _registeredKeys = new();
    private bool _isBuilt;

    public static VFXManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildPools();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceOnLoad()
    {
        EnsureInstance();
    }

    public static VFXManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        var existing = FindObjectOfType<VFXManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var root = new GameObject("VFXManager");
        Instance = root.AddComponent<VFXManager>();
        return Instance;
    }

    public void Play(VFXRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.VFXKey)) return;

        if (!_isBuilt) BuildPools();

        var preparedRequest = PrepareRequest(request);
        var duration = GetDuration(preparedRequest);
        if (!_pool.TryPlay(preparedRequest, duration, this))
            Debug.LogWarning($"[VFXManager] Missing VFX entry for key: {request.VFXKey}");
    }

    private VFXRequest PrepareRequest(VFXRequest request)
    {
        var effectConfig = GetEffectConfigByKey(request.VFXKey);
        var profile = ConfigLoader.GetVfxArtProfile(request.StyleKey);

        if (request.ScaleMultiplier <= 0f) request.ScaleMultiplier = 1f;
        if (request.WidthMultiplier <= 0f) request.WidthMultiplier = 1f;
        if (request.Intensity <= 0f) request.Intensity = 1f;

        if (effectConfig != null && effectConfig.Scale > 0f)
        {
            request.ScaleMultiplier *= effectConfig.Scale;
        }

        if (profile != null)
        {
            request.ScaleMultiplier *= Mathf.Max(0.1f, profile.ScaleMultiplier);
            request.WidthMultiplier *= Mathf.Max(0.1f, profile.WidthMultiplier);

            if (request.Length <= 0f)
            {
                request.Length = Mathf.Max(0f, profile.Length);
            }

            request.Intensity *= Mathf.Max(0.1f, profile.Intensity);

            if (request.Duration <= 0f && effectConfig != null)
            {
                request.Duration = Mathf.Max(0.05f, effectConfig.Duration * Mathf.Max(0.1f, profile.DurationMultiplier));
            }
            else if (request.Duration > 0f)
            {
                request.Duration *= Mathf.Max(0.1f, profile.DurationMultiplier);
            }

            if (request.PrimaryColor == default)
            {
                request.PrimaryColor = VFXPaletteUtility.ParseColor(profile.PrimaryColorHex, Color.white);
            }

            if (request.AccentColor == default)
            {
                request.AccentColor = VFXPaletteUtility.ParseColor(profile.AccentColorHex, request.PrimaryColor);
            }
        }

        if (request.PrimaryColor == default) request.PrimaryColor = Color.white;
        if (request.AccentColor == default) request.AccentColor = request.PrimaryColor;

        return request;
    }

    private void BuildPools()
    {
        if (_isBuilt) return;

        foreach (var entry in vfxEntries) RegisterEntry(entry);

        foreach (var effectConfig in ConfigLoader.GetAllEffectConfigs())
        {
            if (effectConfig == null || string.IsNullOrWhiteSpace(effectConfig.EffectKey)) continue;

            if (_registeredKeys.Contains(effectConfig.EffectKey)) continue;

            var prefab = Resources.Load<GameObject>($"VFX/Prefabs/{effectConfig.PrefabName}");
            if (prefab == null) continue;

            RegisterEntry(new VFXEntry
            {
                vfxKey = effectConfig.EffectKey,
                prefab = prefab,
                effectConfigId = effectConfig.EffectID,
                fallbackDuration = effectConfig.Duration,
                fallbackWarmupCount = effectConfig.WarmupCount
            });
        }

        _isBuilt = true;
    }

    private void RegisterEntry(VFXEntry entry)
    {
        if (entry == null || entry.prefab == null || string.IsNullOrWhiteSpace(entry.vfxKey) ||
            _registeredKeys.Contains(entry.vfxKey)) return;

        var particle = entry.prefab.GetComponent<ParticleSystem>();
        var custom = entry.prefab.GetComponent<VFXBase>();

        if (particle != null)
        {
            _pool.Register(entry.vfxKey, particle);
        }
        else if (custom != null)
        {
            _pool.Register(entry.vfxKey, custom);
        }
        else
        {
            Debug.LogWarning($"[VFXManager] Prefab {entry.prefab.name} does not contain ParticleSystem or VFXBase.");
            return;
        }

        _registeredKeys.Add(entry.vfxKey);
        _pool.Prewarm(entry.vfxKey, GetWarmupCount(entry));
    }

    private int GetWarmupCount(VFXEntry entry)
    {
        var effectConfig = entry.effectConfigId > 0 ? ConfigLoader.GetEffectConfig(entry.effectConfigId) : null;
        return effectConfig != null ? Mathf.Max(0, effectConfig.WarmupCount) : Mathf.Max(0, entry.fallbackWarmupCount);
    }

    private float GetDuration(VFXRequest request)
    {
        if (request.Duration > 0f)
        {
            return request.Duration;
        }

        foreach (var entry in vfxEntries)
        {
            if (entry.vfxKey != request.VFXKey) continue;

            var effectConfig = entry.effectConfigId > 0 ? ConfigLoader.GetEffectConfig(entry.effectConfigId) : null;
            return effectConfig != null && effectConfig.Duration > 0f
                ? effectConfig.Duration
                : Mathf.Max(0.01f, entry.fallbackDuration);
        }

        foreach (var effectConfig in ConfigLoader.GetAllEffectConfigs())
            if (effectConfig != null && effectConfig.EffectKey == request.VFXKey)
                return Mathf.Max(0.01f, effectConfig.Duration);

        return 0.5f;
    }

    private EffectConfig GetEffectConfigByKey(string vfxKey)
    {
        foreach (var effectConfig in ConfigLoader.GetAllEffectConfigs())
        {
            if (effectConfig != null && effectConfig.EffectKey == vfxKey)
            {
                return effectConfig;
            }
        }

        return null;
    }

    [Serializable]
    public class VFXEntry
    {
        public string vfxKey;
        public GameObject prefab;
        public int effectConfigId;
        public float fallbackDuration = 0.5f;
        public int fallbackWarmupCount = 8;
    }
}
