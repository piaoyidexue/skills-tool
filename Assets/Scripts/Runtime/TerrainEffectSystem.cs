using System.Collections.Generic;
using UnityEngine;

public class TerrainEffectSystem : MonoBehaviour
{
    private readonly List<TerrainRuntime> _terrains = new();

    public static TerrainEffectSystem Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureLoaded()
    {
        EnsureInstance();
    }

    public static TerrainEffectSystem EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        var existing = FindObjectOfType<TerrainEffectSystem>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var root = new GameObject("TerrainEffectSystem");
        Instance = root.AddComponent<TerrainEffectSystem>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        for (var i = _terrains.Count - 1; i >= 0; i--)
        {
            _terrains[i].Remaining -= Time.deltaTime;
            if (_terrains[i].Remaining <= 0f)
            {
                _terrains.RemoveAt(i);
            }
        }
    }

    public void Paint(string terrainTag, Vector3 position, float radius, float duration)
    {
        if (string.IsNullOrWhiteSpace(terrainTag))
        {
            return;
        }

        _terrains.Add(new TerrainRuntime
        {
            TerrainTag = terrainTag,
            Position = position,
            Radius = radius,
            Duration = duration,
            Remaining = duration
        });
    }

    public bool HasTerrainNear(Vector3 position, string terrainTag, float searchRadius)
    {
        for (var i = 0; i < _terrains.Count; i++)
        {
            var terrain = _terrains[i];
            if (!terrain.IsActive || terrain.TerrainTag != terrainTag)
            {
                continue;
            }

            if (Vector3.Distance(terrain.Position, position) <= terrain.Radius + searchRadius)
            {
                return true;
            }
        }

        return false;
    }
}
