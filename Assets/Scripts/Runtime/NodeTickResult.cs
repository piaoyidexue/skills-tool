/// <summary>
///     节点 Tick 返回值 —— 0 GC 状态机驱动核心。
///     替代 IEnumerator + yield return 模式。
/// </summary>
public enum NodeTickResult
{
    /// <summary>本帧未完成，下一帧继续 Tick</summary>
    Running,

    /// <summary>执行成功，推进到下一节点</summary>
    Success,

    /// <summary>执行失败，停止当前执行链</summary>
    Failure
}
