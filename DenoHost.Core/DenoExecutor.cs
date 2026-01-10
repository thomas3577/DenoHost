using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DenoHost.Core;

/// <summary>
/// Internal executor for Deno processes. Handles the core execution logic.
/// </summary>
internal static class DenoExecutor
{
  /// <summary>
  /// Test hook: invoked after the Deno process is started.
  /// </summary>
  internal static Action<int>? ProcessStartedCallback { get; set; }

  /// <summary>
  /// Executes a Deno process with the specified parameters and returns the result as type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The expected return type of the Deno process.</typeparam>
  /// <param name="workingDirectory">The working directory for the process.</param>
  /// <param name="command">The Deno command.</param>
  /// <param name="resultType">The type to deserialize the result to.</param>
  /// <param name="jsonSerializerOptions">Optional JSON serializer options.</param>
  /// <param name="logger">Optional logger instance.</param>
  /// <param name="args">Additional arguments for Deno.</param>
  /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
  /// <returns>The deserialized result of the Deno process.</returns>
  /// <exception cref="ArgumentNullException">Thrown if resultType is null.</exception>
  /// <exception cref="NotSupportedException">Thrown if dynamic types are used.</exception>
  /// <exception cref="ArgumentException">Thrown if no command or arguments are provided.</exception>
  /// <exception cref="InvalidOperationException">Thrown if Deno execution fails or deserialization fails.</exception>
  /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
  internal static async Task<T> Execute<T>(
    string? workingDirectory,
    string? command,
    Type? resultType,
    JsonSerializerOptions? jsonSerializerOptions,
    ILogger? logger,
    string[]? args,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(resultType);

    // Dynamic types are not supported
    if (resultType == typeof(object) || resultType.Name == "Object")
    {
      throw new NotSupportedException(
        "Dynamic types are not supported. Use JsonElement, Dictionary<string, object>, or a concrete class instead.");
    }

    var stopwatch = Stopwatch.StartNew();

    // Use provided logger or fall back to global Deno.Logger
    var effectiveLogger = logger ?? Deno.Logger;

    try
    {
      workingDirectory ??= Directory.GetCurrentDirectory();

      var fileName = Helper.GetDenoPath();
      var argumentList = Helper.BuildArgumentsArray(args, command);

      if (argumentList.Length == 0)
        throw new ArgumentException("No command or arguments provided for Deno execution.");

      var argumentsForLog = string.Join(' ', argumentList);

      effectiveLogger?.LogInformation(LogEvents.DenoExecutionStarted,
        "Starting Deno execution: {Arguments} | Working Directory: {WorkingDirectory}",
        argumentsForLog, workingDirectory);

      var startInfo = new ProcessStartInfo
      {
        WorkingDirectory = workingDirectory,
        FileName = fileName,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = System.Text.Encoding.UTF8,
        StandardErrorEncoding = System.Text.Encoding.UTF8
      };

      foreach (var arg in argumentList)
        startInfo.ArgumentList.Add(arg);

      using var process = new Process
      {
        StartInfo = startInfo
      };

      process.Start();

      ProcessStartedCallback?.Invoke(process.Id);

      using var cancellationRegistration = cancellationToken.Register(() =>
      {
        try
        {
          if (!process.HasExited)
          {
            effectiveLogger?.LogWarning(LogEvents.DenoExecutionError,
              "Cancellation requested. Terminating Deno process...");
            process.Kill(entireProcessTree: true);
          }
        }
        catch
        {
          // Best-effort: process may have already exited/disposed.
        }
      });

      var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
      var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
      var waitForExitTask = process.WaitForExitAsync(cancellationToken);

      await Task.WhenAll(outputTask, errorTask, waitForExitTask).ConfigureAwait(false);

      string output = outputTask.Result;
      string error = errorTask.Result;
      stopwatch.Stop();

      if (process.ExitCode != 0)
      {
        effectiveLogger?.LogError(LogEvents.DenoExecutionFailed,
          "Deno execution failed with exit code {ExitCode} after {ElapsedMs}ms. Error: {Error}",
          process.ExitCode, stopwatch.ElapsedMilliseconds, error);

        throw new InvalidOperationException(
          $"Deno exited with code {process.ExitCode}.{Environment.NewLine}" +
          $"Standard Output:{Environment.NewLine}{output}{Environment.NewLine}" +
          $"Standard Error:{Environment.NewLine}{error}"
        );
      }

      effectiveLogger?.LogInformation(LogEvents.DenoExecutionCompleted,
        "Deno execution completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
      effectiveLogger?.LogDebug(LogEvents.DenoOutput, "Deno output: {Output}", output);

      var typeName = typeof(T).Name;
      var deserializedResult = typeName == "String" ? output : JsonSerializer.Deserialize(output, resultType, jsonSerializerOptions);

      return deserializedResult != null
        ? (T)deserializedResult
        : throw new InvalidOperationException("Deserialization returned null.");
    }
    catch (OperationCanceledException ex)
    {
      stopwatch.Stop();
      effectiveLogger?.LogWarning(LogEvents.DenoExecutionError, ex,
        "Deno execution was cancelled after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

      // Preserve cancellation semantics for callers.
      throw;
    }
    catch (Exception ex)
    {
      stopwatch.Stop();
      effectiveLogger?.LogError(LogEvents.DenoExecutionError, ex,
        "Deno execution encountered an error after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
      throw new InvalidOperationException($"An error occurred during Deno execution after {stopwatch.ElapsedMilliseconds}ms. See inner exception for details.", ex);
    }
  }
}
