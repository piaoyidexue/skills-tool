using System.Collections.Generic;

public interface IStatusReceiver
{
    void ApplyStatus(StatusRuntime status);
    bool HasStatus(StatusType type);
    bool TryGetStatus(StatusType type, out StatusRuntime status);
    bool ConsumeStatus(StatusType type, out StatusRuntime status);
    IReadOnlyCollection<StatusRuntime> GetActiveStatuses();
}
