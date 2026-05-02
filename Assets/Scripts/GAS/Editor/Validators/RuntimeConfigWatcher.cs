#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    private static readonly Dictionary<string, DateTime> _recentChanges = new();
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(500);

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
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }

            _watcher = new FileSystemWatcher(_watchPath, "*.csv")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnConfigFileChanged;
            _watcher.Created += OnConfigFileChanged;
            _watcher.Error += OnWatcherError;

            Debug.Log($"[HotReload] Watching config directory: {_watchPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HotReload] Failed to start file watcher: {ex.Message}");
            CleanupWatcher();
        }
    }

    private static void StopWatching()
    {
        CleanupWatcher();
        Debug.Log("[HotReload] Stopped watching config directory.");
    }

    private static void CleanupWatcher()
    {
        if (_watcher != null)
        {
            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnConfigFileChanged;
                _watcher.Created -= OnConfigFileChanged;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
            }
            catch
            {
            }
            _watcher = null;
        }
    }

    private static void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        if (ex is InternalBufferOverflowException)
        {
            Debug.LogWarning("[HotReload] Buffer overflow - too many file changes. Consider reducing notification frequency.");
        }
        else
        {
            Debug.LogError($"[HotReload] FileSystemWatcher error: {ex.Message}");
        }

        EditorApplication.delayCall += StopWatching;
        EditorApplication.delayCall += StartWatching;
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        var fileName = Path.GetFileName(e.FullPath);
        var now = DateTime.Now;

        lock (_recentChanges)
        {
            if (_recentChanges.TryGetValue(fileName, out var lastChange))
            {
                if (now - lastChange < DebounceInterval)
                {
                    return;
                }
            }
            _recentChanges[fileName] = now;
        }

        EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying) return;

            ConfigLoader.ReloadAll();
            Debug.Log($"[HotReload] Config reloaded due to: {fileName}");
        };
    }
}
#endif
