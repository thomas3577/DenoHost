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
  public static readonly EventId SignatureValidationFailed = new(2001, "SignatureValidationFailed");
  public static readonly EventId HashMismatchDetected = new(2002, "HashMismatchDetected");
  public static readonly EventId MetadataMissing = new(2003, "MetadataMissing");
  public static readonly EventId StrictModeBypassBlocked = new(2004, "StrictModeBypassBlocked");
  public static readonly EventId SecurityBypassUsed = new(2005, "SecurityBypassUsed");
}
