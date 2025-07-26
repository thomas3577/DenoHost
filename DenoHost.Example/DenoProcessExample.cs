using DenoHost.Core;

namespace DenoHost.Example;

/// <summary>
/// Demonstrates the usage of the DenoProcess class for managing long-running Deno processes.
/// </summary>
public static class DenoProcessExample
{
  /// <summary>
  /// Example showing basic DenoProcess usage with a simple script.
  /// </summary>
  public static async Task BasicUsageExample(ILogger logger)
  {
    logger.LogInformation("=== Basic DenoProcess Usage Example ===");

    // Create a DenoProcess instance
    using var denoProcess = new DenoProcess(
      command: "run",
      args: ["--allow-read", "scripts/long-running.ts"],
      workingDirectory: Directory.GetCurrentDirectory(),
      logger: logger
    );

    // Subscribe to events
    denoProcess.OutputDataReceived += (sender, e) =>
    {
      logger.LogInformation("Output: {Data}", e.Data);
    };

    denoProcess.ErrorDataReceived += (sender, e) =>
    {
      logger.LogError("Error: {Data}", e.Data);
    };

    denoProcess.ProcessExited += (sender, e) =>
    {
      logger.LogInformation("Process exited with code: {ExitCode}", e.ExitCode);
    };

    try
    {
      // Start the process
      await denoProcess.StartAsync();
      logger.LogInformation("Process started with PID: {ProcessId}", denoProcess.ProcessId);

      // Let it run for a while
      await Task.Delay(5000);

      // Send some input if the script supports it
      if (denoProcess.IsRunning)
      {
        await denoProcess.SendInputAsync("Hello from C#!");
      }

      // Wait a bit more
      await Task.Delay(2000);

      // Stop the process gracefully
      await denoProcess.StopAsync(timeout: TimeSpan.FromSeconds(10));
      logger.LogInformation("Process stopped gracefully");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error occurred during process execution");
    }
  }

  /// <summary>
  /// Example showing how to restart a DenoProcess.
  /// </summary>
  public static async Task RestartExample(ILogger logger)
  {
    logger.LogInformation("=== DenoProcess Restart Example ===");

    using var denoProcess = new DenoProcess(
      command: "run",
      args: ["--allow-read", "scripts/app.ts"],
      logger: logger
    );

    try
    {
      // Start the process
      await denoProcess.StartAsync();
      logger.LogInformation("Process started");

      // Let it run
      await Task.Delay(3000);

      // Restart the process
      await denoProcess.RestartAsync();
      logger.LogInformation("Process restarted");

      // Let it run again
      await Task.Delay(2000);

      // Stop it
      await denoProcess.StopAsync();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error occurred during restart example");
    }
  }

  /// <summary>
  /// Example showing how to wait for a process to complete.
  /// </summary>
  public static async Task WaitForExitExample(ILogger logger)
  {
    logger.LogInformation("=== DenoProcess WaitForExit Example ===");

    using var denoProcess = new DenoProcess(
      command: "run",
      args: ["scripts/finite-task.ts"],
      logger: logger
    );

    try
    {
      await denoProcess.StartAsync();
      logger.LogInformation("Process started, waiting for completion...");

      // Wait for the process to complete naturally
      var exitCode = await denoProcess.WaitForExitAsync();
      logger.LogInformation("Process completed with exit code: {ExitCode}", exitCode);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error occurred during wait example");
    }
  }

  /// <summary>
  /// Example showing error handling when a process fails to start.
  /// </summary>
  public static async Task ErrorHandlingExample(ILogger logger)
  {
    logger.LogInformation("=== DenoProcess Error Handling Example ===");

    using var denoProcess = new DenoProcess(
      command: "run",
      args: ["non-existent-script.ts"],
      logger: logger
    );

    try
    {
      await denoProcess.StartAsync();
      logger.LogInformation("This should not be reached if script doesn't exist");
    }
    catch (InvalidOperationException ex)
    {
      logger.LogWarning("Expected error occurred: {Message}", ex.Message);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Unexpected error occurred");
    }
  }

  /// <summary>
  /// Example showing interactive communication with a Deno process.
  /// </summary>
  public static async Task InteractiveExample(ILogger logger)
  {
    logger.LogInformation("=== DenoProcess Interactive Example ===");

    using var denoProcess = new DenoProcess(
      command: "run",
      args: ["--allow-read", "scripts/interactive.ts"],
      logger: logger
    );

    var responses = new List<string>();

    // Capture output
    denoProcess.OutputDataReceived += (sender, e) =>
    {
      if (!string.IsNullOrEmpty(e.Data))
      {
        responses.Add(e.Data);
        logger.LogInformation("Received: {Data}", e.Data);
      }
    };

    try
    {
      await denoProcess.StartAsync();

      // Send a series of commands
      var commands = new[] { "command1", "command2", "exit" };

      foreach (var command in commands)
      {
        await Task.Delay(1000); // Wait a bit between commands
        await denoProcess.SendInputAsync(command);
        logger.LogInformation("Sent: {Command}", command);
      }

      // Wait for the process to exit
      await denoProcess.WaitForExitAsync();
      logger.LogInformation("Interactive session completed. Received {Count} responses.", responses.Count);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error occurred during interactive example");
    }
  }
}
