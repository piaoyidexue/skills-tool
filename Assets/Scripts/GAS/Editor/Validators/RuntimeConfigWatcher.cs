#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
///     运行时配置热重载监听器 —— Play 模式下监听 CSV 文件变更，
///     自动调用 ConfigLoader.ReloadAll() 实现数值热替换。
///     无需重启 Play 模式即可看到修改后的数值。
/// </summary>
[InitializeOnLoad]
public class RuntimeConfigWatcher
{
    private static FileSystemWatcher _watcher;
    private static string _watchPath;

    static RuntimeConfigWatcher()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                StartWatching();
                break;
            case PlayModeStateChange.ExitingPlayMode:
                StopWatching();
                break;
        }
    }

    private static void StartWatching()
    {
        _watchPath = Path.Combine(Application.dataPath, "Resources/Config");
        if (!Directory.Exists(_watchPath))
        {
            Debug.LogWarning($"[HotReload] Config directory not found: {_watchPath}");
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(_watchPath, "*.csv")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnConfigFileChanged;
            _watcher.Created += OnConfigFileChanged;

            Debug.Log($"[HotReload] Watching config directory: {_watchPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HotReload] Failed to start file watcher: {ex.Message}");
        }
    }

    private static void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        Debug.Log("[HotReload] Stopped watching config directory.");
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // 在主线程执行 Reload
        EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying) return;

            ConfigLoader.ReloadAll();
            Debug.Log($"[HotReload] Config reloaded due to: {Path.GetFileName(e.FullPath)}");
        };
    }
}
#endif
