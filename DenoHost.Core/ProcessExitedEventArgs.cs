using System;

namespace DenoHost.Core;

/// <summary>
/// Provides data for the ProcessExited event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ProcessExitedEventArgs"/> class.
/// </remarks>
/// <param name="exitCode">The exit code of the process.</param>
public class ProcessExitedEventArgs(int exitCode) : EventArgs
{
    /// <summary>
    /// Gets the exit code of the process.
    /// </summary>
    public int ExitCode { get; } = exitCode;
}
