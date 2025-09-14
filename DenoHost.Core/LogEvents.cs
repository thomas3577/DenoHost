using Microsoft.Extensions.Logging;

namespace DenoHost.Core;

/// <summary>
/// Event IDs for structured logging.
/// </summary>
internal static class LogEvents
{
    public static readonly EventId DenoExecutionStarted = new(1001, "DenoExecutionStarted");
    public static readonly EventId DenoExecutionCompleted = new(1002, "DenoExecutionCompleted");
    public static readonly EventId DenoExecutionFailed = new(1003, "DenoExecutionFailed");
    public static readonly EventId DenoExecutionError = new(1004, "DenoExecutionError");
    public static readonly EventId DenoOutput = new(1005, "DenoOutput");
}
