using System.Text.Json;
using DenoHost.Core;
using Microsoft.Extensions.Logging;

namespace DenoHost.Example;

/// <summary>
/// Demonstrates the bidirectional API bridge feature of DenoProcess.
/// This example shows how to use structured JSON-RPC communication
/// between .NET and Deno processes.
/// </summary>
public class StructuredCommunicationExample
{
  private readonly ILogger<StructuredCommunicationExample> _logger;

  public StructuredCommunicationExample(ILogger<StructuredCommunicationExample> logger)
  {
    _logger = logger;
  }

  /// <summary>
  /// Runs the structured communication example.
  /// </summary>
  public async Task RunAsync()
  {
    // Create a DenoProcess for structured communication
    var denoProcess = new DenoProcess(
        command: "run",
        args: ["--allow-all", "structured-bridge-example.ts"],
        workingDirectory: Directory.GetCurrentDirectory(),
        logger: _logger
    );

    // Register methods that can be called from Deno
    RegisterDotNetMethods(denoProcess);

    // Subscribe to events
    denoProcess.StructuredMessageReceived += OnStructuredMessageReceived;
    denoProcess.ProcessExited += OnProcessExited;

    try
    {
      _logger.LogInformation("Starting Deno process with structured communication...");
      await denoProcess.StartAsync();

      // Wait a moment for Deno to initialize
      await Task.Delay(2000);

      // Demonstrate calling Deno methods from .NET
      await DemonstrateDotNetToDenoCallsAsync(denoProcess);

      // Keep the process running for a while to see heartbeats
      _logger.LogInformation("Listening for heartbeats for 30 seconds...");
      await Task.Delay(30000);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error running structured communication example");
    }
    finally
    {
      await denoProcess.StopAsync();
      denoProcess.Dispose();
    }
  }

  private void RegisterDotNetMethods(DenoProcess denoProcess)
  {
    // Register a simple greeting method
    denoProcess.RegisterMethod("dotnet.greet", async (parameters) =>
    {
      var name = parameters.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "World";
      var greeting = $"Hello {name} from .NET!";
      _logger.LogInformation("Greeting method called with name: {Name}", name);

      return JsonSerializer.SerializeToElement(new
      {
        greeting = greeting,
        timestamp = DateTime.UtcNow,
        source = "DenoHost.Example"
      });
    });

    // Register a calculation method
    denoProcess.RegisterMethod("dotnet.calculate", async (parameters) =>
    {
      var operation = parameters.GetProperty("operation").GetString();
      var numbers = parameters.GetProperty("numbers").EnumerateArray()
              .Select(x => x.GetDouble()).ToArray();

      double result = operation?.ToLower() switch
      {
        "sum" => numbers.Sum(),
        "product" => numbers.Aggregate(1.0, (a, b) => a * b),
        "average" => numbers.Average(),
        "max" => numbers.Max(),
        "min" => numbers.Min(),
        _ => throw new ArgumentException($"Unknown operation: {operation}")
      };

      _logger.LogInformation("Calculation: {Operation} of [{Numbers}] = {Result}",
              operation, string.Join(", ", numbers), result);

      return JsonSerializer.SerializeToElement(new
      {
        operation = operation,
        numbers = numbers,
        result = result,
        calculatedAt = DateTime.UtcNow
      });
    });

    // Register a data processing method
    denoProcess.RegisterMethod("dotnet.processData", async (parameters) =>
    {
      var data = parameters.GetProperty("data").EnumerateArray()
              .Select(x => x.GetString()).ToArray();

      var processed = data
              .Where(x => !string.IsNullOrWhiteSpace(x))
              .Select(x => x?.Trim().ToUpperInvariant())
              .OrderBy(x => x)
              .ToArray();

      _logger.LogInformation("Processed {Count} data items", processed.Length);

      return JsonSerializer.SerializeToElement(new
      {
        originalCount = data.Length,
        processedCount = processed.Length,
        processedData = processed,
        processedAt = DateTime.UtcNow
      });
    });
  }

  private async Task DemonstrateDotNetToDenoCallsAsync(DenoProcess denoProcess)
  {
    try
    {
      _logger.LogInformation("Demonstrating .NET to Deno method calls...");

      // Call math operations
      var addResult = await denoProcess.CallMethodAsync("math.add",
          JsonSerializer.SerializeToElement(new { a = 15, b = 27 }));
      _logger.LogInformation("Math.add result: {Result}", addResult.GetInt32());

      var multiplyResult = await denoProcess.CallMethodAsync("math.multiply",
          JsonSerializer.SerializeToElement(new { a = 7, b = 8 }));
      _logger.LogInformation("Math.multiply result: {Result}", multiplyResult.GetInt32());

      // Get system information
      var systemInfo = await denoProcess.CallMethodAsync("system.info");
      _logger.LogInformation("Deno system info: {Info}", systemInfo);

      // Test file operations (create temp file)
      var tempFile = Path.GetTempFileName();
      var testContent = "Hello from .NET via Deno!";

      await denoProcess.CallMethodAsync("file.write",
          JsonSerializer.SerializeToElement(new { path = tempFile, content = testContent }));
      _logger.LogInformation("Wrote test file: {TempFile}", tempFile);

      var fileResult = await denoProcess.CallMethodAsync("file.read",
          JsonSerializer.SerializeToElement(new { path = tempFile }));
      _logger.LogInformation("Read file result: {Content}",
          fileResult.GetProperty("content").GetString());

      // Clean up temp file
      File.Delete(tempFile);

      // Test HTTP request
      var httpResult = await denoProcess.CallMethodAsync("http.get",
          JsonSerializer.SerializeToElement(new { url = "https://httpbin.org/json" }));
      _logger.LogInformation("HTTP request status: {Status}",
          httpResult.GetProperty("status").GetInt32());

      // Send notifications to Deno
      await denoProcess.SendNotificationAsync("dotnet.status",
          JsonSerializer.SerializeToElement(new
          {
            message = "Demo completed successfully",
            timestamp = DateTime.UtcNow,
            source = "StructuredCommunicationExample"
          }));

    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during .NET to Deno calls demonstration");
    }
  }

  private void OnStructuredMessageReceived(object? sender, StructuredMessageEventArgs e)
  {
    switch (e.Message)
    {
      case JsonRpcNotification notification:
        HandleNotification(notification);
        break;
      case JsonRpcRequest request:
        _logger.LogInformation("Received request from Deno: {Method}", request.Method);
        break;
      case JsonRpcResponse response:
        _logger.LogInformation("Received response from Deno for request: {Id}", response.Id);
        break;
    }
  }

  private void HandleNotification(JsonRpcNotification notification)
  {
    switch (notification.Method)
    {
      case "deno.ready":
        var methods = notification.Params?.GetProperty("methods").EnumerateArray()
            .Select(x => x.GetString()).ToArray() ?? Array.Empty<string>();
        _logger.LogInformation("Deno process ready! Available methods: [{Methods}]",
            string.Join(", ", methods));
        break;

      case "deno.heartbeat":
        var timestamp = notification.Params?.GetProperty("timestamp").GetString();
        var memory = notification.Params?.TryGetProperty("memoryUsage", out var memProp) == true
            ? memProp.ToString() : "unknown";
        _logger.LogDebug("Deno heartbeat at {Timestamp}, memory: {Memory}", timestamp, memory);
        break;

      default:
        _logger.LogInformation("Received notification: {Method} with params: {Params}",
            notification.Method, notification.Params);
        break;
    }
  }

  private void OnProcessExited(object? sender, ProcessExitedEventArgs e)
  {
    _logger.LogInformation("Deno process exited with code: {ExitCode}", e.ExitCode);
  }
}
