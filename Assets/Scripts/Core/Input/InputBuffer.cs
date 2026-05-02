using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 0 GC 环形缓冲区实现 - 用于输入指令缓存
/// </summary>
public struct InputBuffer
{
    private readonly InputCommand[] _buffer;
    private readonly int _capacity;
    private int _head;
    private int _tail;
    private int _count;
    
    public int Capacity => _capacity;
    public int Count => _count;
    
    public InputBuffer(int capacity)
    {
        _capacity = capacity;
        _buffer = new InputCommand[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }
    
    /// <summary>
    /// 尝试添加指令到缓冲区
    /// </summary>
    /// <returns>是否成功添加</returns>
    public bool TryEnqueue(InputCommand command)
    {
        if (_count >= _capacity) return false;
        
        _buffer[_tail] = command;
        _tail = (_tail + 1) % _capacity;
        _count++;
        return true;
    }
    
    /// <summary>
    /// 尝试从缓冲区头部获取指令（不移除）
    /// </summary>
    /// <returns>是否成功获取</returns>
    public bool TryPeek(out InputCommand command)
    {
        if (_count == 0)
        {
            command = default;
            return false;
        }
        
        command = _buffer[_head];
        return true;
    }
    
    /// <summary>
    /// 尝试从缓冲区头部移除指令
    /// </summary>
    /// <returns>是否成功移除</returns>
    public bool TryDequeue(out InputCommand command)
    {
        if (_count == 0)
        {
            command = default;
            return false;
        }
        
        command = _buffer[_head];
        _head = (_head + 1) % _capacity;
        _count--;
        return true;
    }
    
    /// <summary>
    /// 清空缓冲区
    /// </summary>
    public void Clear()
    {
        _head = 0;
        _tail = 0;
        _count = 0;
    }
    
    /// <summary>
    /// 检查是否有过期的指令（基于时间戳）
    /// </summary>
    /// <param name="maxAgeSeconds">最大允许年龄（秒）</param>
    /// <returns>是否有有效指令</returns>
    public bool HasValidCommand(float maxAgeSeconds)
    {
        if (_count == 0) return false;
        
        var oldestCommand = _buffer[_head];
        return Time.time - oldestCommand.Timestamp <= maxAgeSeconds;
    }
}

/// <summary>
/// 输入缓冲区管理器 - 单例，全局访问
/// </summary>
public class InputBufferManager : MonoBehaviour
{
    private static InputBufferManager _instance;
    public static InputBufferManager Instance => _instance;
    
    [Header("Input Buffer Settings")]
    [Tooltip("输入缓冲区容量（默认32个指令）")]
    [SerializeField] private int _bufferCapacity = 32;
    
    [Tooltip("输入指令最大有效时间（秒），超过此时间的指令会被忽略")]
    [SerializeField] private float _maxCommandAge = 0.2f;
    
    private InputBuffer _inputBuffer;
    
    public InputBuffer InputBuffer => _inputBuffer;
    public float MaxCommandAge => _maxCommandAge;
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        _inputBuffer = new InputBuffer(_bufferCapacity);
    }
    
    /// <summary>
    /// 尝试添加输入指令到缓冲区
    /// </summary>
    public bool TryAddCommand(InputCommand command)
    {
        return _inputBuffer.TryEnqueue(command);
    }
    
    /// <summary>
    /// 尝试获取并移除最老的有效指令
    /// </summary>
    /// <returns>是否成功获取有效指令</returns>
    public bool TryGetValidCommand(out InputCommand command)
    {
        if (!_inputBuffer.TryPeek(out command))
        {
            command = default;
            return false;
        }
        
        // 检查是否过期
        if (Time.time - command.Timestamp > _maxCommandAge)
        {
            // 移除过期指令
            _inputBuffer.TryDequeue(out _);
            return TryGetValidCommand(out command);
        }
        
        // 获取并移除
        _inputBuffer.TryDequeue(out command);
        return true;
    }
    
    /// <summary>
    /// 清空缓冲区
    /// </summary>
    public void ClearBuffer()
    {
        _inputBuffer.Clear();
    }
}