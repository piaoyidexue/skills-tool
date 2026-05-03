using UnityEngine;

/// <summary>
///     黑板组件 —— Blackboard 类的 MonoBehaviour 封装器。
///     遵循项目规范：Blackboard 不能直接暴露在外部逻辑中，必须通过此组件访问。
/// </summary>
public class BlackboardComponent : MonoBehaviour
{
    /// <summary>内部黑板实例（私有，禁止外部直接访问）</summary>
    private Blackboard _blackboard;

    /// <summary>公开的黑板访问器（只读）</summary>
    public Blackboard Blackboard => _blackboard;

    /// <summary>
    ///     初始化。
    /// </summary>
    private void Awake()
    {
        _blackboard = new Blackboard();
    }

    /// <summary>
    ///     安全设置黑板值。
    /// </summary>
    public void SetValue<T>(string key, T value)
    {
        if (_blackboard != null)
            _blackboard.SetValue(key, value);
    }

    /// <summary>
    ///     安全获取黑板值。
    /// </summary>
    public T GetValue<T>(string key, T defaultValue = default)
    {
        if (_blackboard != null)
            return _blackboard.GetValue(key, defaultValue);
        return defaultValue;
    }
}