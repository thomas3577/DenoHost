using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DenoHost.Core;

/// <summary>
/// Internal executor for Deno processes. Handles the core execution logic.
/// </summary>
internal static class DenoExecutor
{
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
    /// <returns>The deserialized result of the Deno process.</returns>
    /// <exception cref="ArgumentNullException">Thrown if resultType is null.</exception>
    /// <exception cref="NotSupportedException">Thrown if dynamic types are used.</exception>
    /// <exception cref="ArgumentException">Thrown if no command or arguments are provided.</exception>
    /// <exception cref="InvalidOperationException">Thrown if Deno execution fails or deserialization fails.</exception>
    internal static async Task<T> Execute<T>(
      string? workingDirectory,
      string? command,
      Type? resultType,
      JsonSerializerOptions? jsonSerializerOptions,
      ILogger? logger,
      string[]? args)
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
            var arguments = Helper.BuildArguments(args, command);

            if (string.IsNullOrWhiteSpace(arguments))
                throw new ArgumentException("No command or arguments provided for Deno execution.");

            effectiveLogger?.LogInformation("Command: deno {Arguments} {FileName}", arguments, fileName);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = workingDirectory,
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                }
            };

            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            stopwatch.Stop();

            if (process.ExitCode != 0)
            {
                effectiveLogger?.LogError("Deno execution failed with exit code {ExitCode} after {ElapsedMs}ms. Error: {Error}",
                  process.ExitCode, stopwatch.ElapsedMilliseconds, error);

                throw new InvalidOperationException(
                  $"Deno exited with code {process.ExitCode}.{Environment.NewLine}" +
                  $"Standard Output:{Environment.NewLine}{output}{Environment.NewLine}" +
                  $"Standard Error:{Environment.NewLine}{error}"
                );
            }

            effectiveLogger?.LogInformation("Deno execution completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            effectiveLogger?.LogDebug("Deno output: {Output}", output);

            var typeName = typeof(T).Name;
            var deserializedResult = typeName == "String" ? output : JsonSerializer.Deserialize(output, resultType, jsonSerializerOptions);

            return deserializedResult != null
              ? (T)deserializedResult
              : throw new InvalidOperationException("Deserialization returned null.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            effectiveLogger?.LogError(ex, "Deno execution encountered an error after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            throw new InvalidOperationException($"An error occurred during Deno execution after {stopwatch.ElapsedMilliseconds}ms. See inner exception for details.", ex);
        }
    }
}
