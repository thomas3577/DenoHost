using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DenoHost.Core.IPC;

namespace DenoHost.Core;

/// <summary>
/// Represents a managed Deno process that can be started, controlled, and stopped.
/// Unlike the static Deno class, this provides a long-running process that can be interacted with over time.
/// </summary>
public class DenoProcess : IDisposable
{
  private readonly string _workingDirectory;
  private readonly string[] _args;
  private readonly ILogger? _logger;
  private readonly string? _tempConfigPath;
  private readonly IpcTransportType? _ipcTransportType;
  private readonly string? _ipcEndpointName;
  private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pendingRequests = new();
  private readonly ConcurrentDictionary<string, Func<JsonElement, Task<JsonElement>>> _registeredMethods = new();
  private readonly Lock _lock = new();
  private Process? _process;
  private IIpcTransport? _ipcTransport;
  private int _nextRequestId = 1;
  private bool _disposed;

  /// <summary>
  /// Gets a value indicating whether the Deno process is currently running.
  /// </summary>
  public bool IsRunning
  {
    get
    {
      using (_lock.EnterScope())
      {
        try
        {
          return _process != null && !_process.HasExited;
        }
        catch (InvalidOperationException)
        {
          // Process has been disposed
          return false;
        }
      }
    }
  }

  /// <summary>
  /// Gets the process ID of the running Deno process, or null if not running.
  /// </summary>
  public int? ProcessId
  {
    get
    {
      using (_lock.EnterScope())
      {
        try
        {
          return _process?.Id;
        }
        catch (InvalidOperationException)
        {
          // Process has been disposed
          return null;
        }
      }
    }
  }

  /// <summary>
  /// Gets the exit code of the process if it has exited, or null if still running.
  /// </summary>
  public int? ExitCode
  {
    get
    {
      using (_lock.EnterScope())
      {
        try
        {
          if (_process == null || !_process.HasExited)
            return null;
          return _process.ExitCode;
        }
        catch (InvalidOperationException)
        {
          // Process has been disposed or exit code not available
          return null;
        }
      }
    }
  }

  /// <summary>
  /// Event that is raised when the process exits.
  /// </summary>
  public event EventHandler<ProcessExitedEventArgs>? ProcessExited;

  /// <summary>
  /// Event that is raised when data is received from standard output.
  /// </summary>
  public event EventHandler<DataReceivedEventArgs>? OutputDataReceived;

  /// <summary>
  /// Event that is raised when data is received from standard error.
  /// </summary>
  public event EventHandler<DataReceivedEventArgs>? ErrorDataReceived;

  /// <summary>
  /// Event that is raised when a structured message is received from the Deno process.
  /// This event is only raised when structured communication is enabled.
  /// </summary>
  public event EventHandler<StructuredMessageEventArgs>? StructuredMessageReceived;

  /// <summary>
  /// Initializes a new instance of the <see cref="DenoProcess"/> class.
  /// </summary>
  /// <param name="args">The arguments to pass to the Deno process.</param>
  /// <param name="workingDirectory">The working directory for the process. If null, uses current directory.</param>
  /// <param name="logger">Optional logger for process operations.</param>
  /// <param name="ipcTransportType">The type of IPC transport to use. If null, uses standard streams.</param>
  /// <param name="ipcEndpointName">The name of the IPC endpoint. If null, generates a unique name.</param>
  public DenoProcess(string[] args, string? workingDirectory = null, ILogger? logger = null, IpcTransportType? ipcTransportType = null, string? ipcEndpointName = null)
  {
    _args = args ?? throw new ArgumentNullException(nameof(args));
    _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    _logger = logger ?? Deno.Logger;
    _ipcTransportType = ipcTransportType;
    _ipcEndpointName = ipcEndpointName ?? (ipcTransportType.HasValue ? IpcTransportFactory.GenerateUniqueEndpointName("denohost") : null);
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="DenoProcess"/> class.
  /// </summary>
  /// <param name="command">The Deno command to execute.</param>
  /// <param name="args">Additional arguments to pass to the Deno process.</param>
  /// <param name="workingDirectory">The working directory for the process. If null, uses current directory.</param>
  /// <param name="logger">Optional logger for process operations.</param>
  /// <param name="ipcTransportType">The type of IPC transport to use. If null, uses standard streams.</param>
  /// <param name="ipcEndpointName">The name of the IPC endpoint. If null, generates a unique name.</param>
  public DenoProcess(string command, string[]? args = null, string? workingDirectory = null, ILogger? logger = null, IpcTransportType? ipcTransportType = null, string? ipcEndpointName = null)
  {
    if (string.IsNullOrWhiteSpace(command))
      throw new ArgumentException("Command cannot be null or empty.", nameof(command));

    var allArgs = Helper.BuildArgumentsArray(args, command);
    _args = allArgs;
    _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    _logger = logger ?? Deno.Logger;
    _ipcTransportType = ipcTransportType;
    _ipcEndpointName = ipcEndpointName ?? (ipcTransportType.HasValue ? IpcTransportFactory.GenerateUniqueEndpointName("denohost") : null);
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="DenoProcess"/> class with base options.
  /// </summary>
  /// <param name="command">The Deno command to execute.</param>
  /// <param name="baseOptions">Base options such as working directory and logger.</param>
  /// <param name="args">Additional arguments to pass to the Deno process.</param>
  /// <param name="ipcTransportType">The type of IPC transport to use. If null, uses standard streams.</param>
  /// <param name="ipcEndpointName">The name of the IPC endpoint. If null, generates a unique name.</param>
  /// <exception cref="ArgumentNullException">Thrown if <paramref name="baseOptions"/> is null.</exception>
  public DenoProcess(string command, DenoExecuteBaseOptions baseOptions, string[]? args = null, IpcTransportType? ipcTransportType = null, string? ipcEndpointName = null)
  {
    ArgumentNullException.ThrowIfNull(baseOptions);

    if (string.IsNullOrWhiteSpace(command))
      throw new ArgumentException("Command cannot be null or empty.", nameof(command));

    var allArgs = Helper.BuildArgumentsArray(args, command);
    _args = allArgs;
    _workingDirectory = baseOptions.WorkingDirectory ?? Directory.GetCurrentDirectory();
    _logger = baseOptions.Logger ?? Deno.Logger;
    _ipcTransportType = ipcTransportType;
    _ipcEndpointName = ipcEndpointName ?? (ipcTransportType.HasValue ? IpcTransportFactory.GenerateUniqueEndpointName("denohost") : null);
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="DenoProcess"/> class with base options.
  /// </summary>
  /// <param name="baseOptions">Base options such as working directory and logger.</param>
  /// <param name="args">Arguments to pass to the Deno process.</param>
  /// <exception cref="ArgumentNullException">Thrown if <paramref name="baseOptions"/> or <paramref name="args"/> is null.</exception>
  public DenoProcess(DenoExecuteBaseOptions baseOptions, string[] args)
  {
    ArgumentNullException.ThrowIfNull(baseOptions);
    ArgumentNullException.ThrowIfNull(args);

    _args = args;
    _workingDirectory = baseOptions.WorkingDirectory ?? Directory.GetCurrentDirectory();
    _logger = baseOptions.Logger ?? Deno.Logger;
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="DenoProcess"/> class with a configuration file or path.
  /// </summary>
  /// <param name="command">The Deno command to execute.</param>
  /// <param name="configOrPath">Configuration as JSON string or path to a configuration file.</param>
  /// <param name="args">Additional arguments to pass to the Deno process.</param>
  /// <param name="workingDirectory">The working directory for the process. If null, uses current directory.</param>
  /// <param name="logger">Optional logger for process operations.</param>
  public DenoProcess(string command, string configOrPath, string[]? args = null, string? workingDirectory = null, ILogger? logger = null)
  {
    if (string.IsNullOrWhiteSpace(command))
      throw new ArgumentException("Command cannot be null or empty.", nameof(command));

    ArgumentNullException.ThrowIfNull(configOrPath);

    var configPath = Helper.EnsureConfigFile(configOrPath);
    var allArgs = Helper.AppendConfigArgument(args ?? [], configPath);
    var commandArgs = Helper.BuildArgumentsArray(allArgs, command);

    _args = commandArgs;
    _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    _logger = logger ?? Deno.Logger;

    // Store config info for cleanup if it's a temp file
    _tempConfigPath = !Helper.IsJsonPathLike(configOrPath) ? configPath : null;
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="DenoProcess"/> class with a configuration object.
  /// </summary>
  /// <param name="command">The Deno command to execute.</param>
  /// <param name="config">The Deno configuration object.</param>
  /// <param name="args">Additional arguments to pass to the Deno process.</param>
  /// <param name="workingDirectory">The working directory for the process. If null, uses current directory.</param>
  /// <param name="logger">Optional logger for process operations.</param>
  /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> is null.</exception>
  public DenoProcess(string command, DenoConfig config, string[]? args = null, string? workingDirectory = null, ILogger? logger = null)
  {
    if (string.IsNullOrWhiteSpace(command))
      throw new ArgumentException("Command cannot be null or empty.", nameof(command));

    ArgumentNullException.ThrowIfNull(config);

    var configPath = Helper.WriteTempConfig(config);
    var allArgs = Helper.AppendConfigArgument(args ?? [], configPath);
    var commandArgs = Helper.BuildArgumentsArray(allArgs, command);

    _args = commandArgs;
    _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    _logger = logger ?? Deno.Logger;

    // Store temp config path for cleanup
    _tempConfigPath = configPath;
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="DenoProcess"/> class with execution options.
  /// </summary>
  /// <param name="options">The execution options for the Deno process.</param>
  /// <param name="workingDirectory">The working directory override. If null, uses options.WorkingDirectory or current directory.</param>
  /// <param name="logger">Logger override. If null, uses options.Logger or Deno.Logger.</param>
  /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
  /// <exception cref="ArgumentException">Thrown if both Config and ConfigOrPath are set.</exception>
  public DenoProcess(DenoExecuteOptions options, string? workingDirectory = null, ILogger? logger = null)
  {
    ArgumentNullException.ThrowIfNull(options);

    var command = options.Command;
    var configOrPath = options.ConfigOrPath;
    var config = options.Config;
    var args = options.Args;

    if (config != null && !string.IsNullOrWhiteSpace(configOrPath))
      throw new ArgumentException("Either 'config' or 'configOrPath' should be provided, not both.");

    if (string.IsNullOrWhiteSpace(command))
      throw new ArgumentException("Command cannot be null or empty.", nameof(command));

    string[]? finalArgs;
    string? tempConfigPath = null;

    if (config != null)
    {
      var configPath = Helper.WriteTempConfig(config);
      var allArgs = Helper.AppendConfigArgument(args ?? [], configPath);
      finalArgs = Helper.BuildArgumentsArray(allArgs, command);
      tempConfigPath = configPath;
    }
    else if (!string.IsNullOrWhiteSpace(configOrPath))
    {
      var configPath = Helper.EnsureConfigFile(configOrPath);
      var allArgs = Helper.AppendConfigArgument(args ?? [], configPath);
      finalArgs = Helper.BuildArgumentsArray(allArgs, command);
      tempConfigPath = !Helper.IsJsonPathLike(configOrPath) ? configPath : null;
    }
    else
    {
      finalArgs = Helper.BuildArgumentsArray(args ?? [], command);
    }

    _args = finalArgs;
    _workingDirectory = workingDirectory ?? options.WorkingDirectory ?? Directory.GetCurrentDirectory();
    _logger = logger ?? options.Logger ?? Deno.Logger;
    _tempConfigPath = tempConfigPath;
  }

  /// <summary>
  /// Starts the Deno process asynchronously.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous start operation.</returns>
  /// <exception cref="InvalidOperationException">Thrown if the process is already running or has been disposed.</exception>
  public async Task StartAsync(CancellationToken cancellationToken = default)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    using (_lock.EnterScope())
    {
      if (_process != null)
        throw new InvalidOperationException("Process is already started. Call Stop() before starting again.");
    }

    var fileName = Helper.GetDenoPath();
    var arguments = string.Join(" ", _args);

    _logger?.LogInformation("Starting Deno process: {FileName} {Arguments}", fileName, arguments);

    var process = new Process
    {
      StartInfo = new ProcessStartInfo
      {
        WorkingDirectory = _workingDirectory,
        FileName = fileName,
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
      },
      EnableRaisingEvents = true
    };

    // Set up event handlers
    process.OutputDataReceived += (sender, e) =>
    {
      if (e.Data != null)
      {
        try
        {
          OutputDataReceived?.Invoke(this, e);
          _logger?.LogDebug("Deno stdout: {Data}", e.Data);
        }
        catch (Exception ex)
        {
          _logger?.LogError(ex, "Error in OutputDataReceived event handler");
        }
      }
    };

    process.ErrorDataReceived += (sender, e) =>
    {
      if (e.Data != null)
      {
        try
        {
          ErrorDataReceived?.Invoke(this, e);
          _logger?.LogDebug("Deno stderr: {Data}", e.Data);
        }
        catch (Exception ex)
        {
          _logger?.LogError(ex, "Error in ErrorDataReceived event handler");
        }
      }
    };

    process.Exited += (sender, e) =>
    {
      try
      {
        var exitCode = process.ExitCode;
        _logger?.LogInformation("Deno process exited with code {ExitCode}", exitCode);
        ProcessExited?.Invoke(this, new ProcessExitedEventArgs(exitCode));
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Error in ProcessExited event handler");
      }
    };

    try
    {
      var started = process.Start();
      if (!started)
        throw new InvalidOperationException("Failed to start Deno process.");

      // Start asynchronous reading of output and error streams
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();

      using (_lock.EnterScope())
      {
        _process = process;
      }

      _logger?.LogInformation("Deno process started successfully with PID {ProcessId}", process.Id);

      // Give the process a moment to initialize
      await Task.Delay(100, cancellationToken);

      // Check if the process is still running after initialization
      // Note: For processes that are meant to exit quickly (like eval scripts),
      // this is expected behavior and not an error
      if (process.HasExited)
      {
        _logger?.LogDebug("Deno process completed quickly with exit code {ExitCode}", process.ExitCode);
      }
      else
      {
        _logger?.LogInformation("Deno process started successfully with PID {ProcessId}", process.Id);
      }
    }
    catch (Exception ex)
    {
      _logger?.LogError(ex, "Failed to start Deno process");
      process.Dispose();
      throw;
    }
  }

  /// <summary>
  /// Sends input to the Deno process's standard input stream.
  /// </summary>
  /// <param name="input">The input to send to the process.</param>
  /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous write operation.</returns>
  /// <exception cref="InvalidOperationException">Thrown if the process is not running.</exception>
  public async Task SendInputAsync(string input, CancellationToken cancellationToken = default)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    Process? process;
    using (_lock.EnterScope())
    {
      process = _process;
    }

    if (process == null || process.HasExited)
      throw new InvalidOperationException("Process is not running.");

    try
    {
      await process.StandardInput.WriteLineAsync(input.AsMemory(), cancellationToken);
      await process.StandardInput.FlushAsync(cancellationToken);
      _logger?.LogDebug("Sent input to Deno process: {Input}", input);
    }
    catch (Exception ex)
    {
      _logger?.LogError(ex, "Failed to send input to Deno process");
      throw;
    }
  }

  /// <summary>
  /// Stops the Deno process gracefully by sending a termination signal.
  /// </summary>
  /// <param name="timeout">Maximum time to wait for graceful shutdown before forcefully killing the process.</param>
  /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous stop operation.</returns>
  public async Task StopAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    Process? process;
    using (_lock.EnterScope())
    {
      process = _process;
      _process = null;
    }

    if (process == null)
    {
      _logger?.LogDebug("Stop requested but process is not running");
      return;
    }

    if (process.HasExited)
    {
      _logger?.LogDebug("Process has already exited with code {ExitCode}", process.ExitCode);
      process.Dispose();
      return;
    }

    var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
    _logger?.LogInformation("Stopping Deno process with PID {ProcessId}, timeout: {Timeout}",
        process.Id, effectiveTimeout);

    try
    {
      // Try graceful shutdown first by closing standard input
      try
      {
        process.StandardInput.Close();
      }
      catch (InvalidOperationException)
      {
        // Process might have already exited or input stream not available
      }

      // Wait for the process to exit gracefully
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      cts.CancelAfter(effectiveTimeout);

      try
      {
        await process.WaitForExitAsync(cts.Token);
        _logger?.LogInformation("Deno process stopped gracefully");
      }
      catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
      {
        // Timeout occurred, force kill the process
        _logger?.LogWarning("Deno process did not stop gracefully within timeout, forcing termination");

        try
        {
          if (!process.HasExited)
          {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
          }
          _logger?.LogInformation("Deno process terminated forcefully");
        }
        catch (Exception ex)
        {
          _logger?.LogError(ex, "Failed to forcefully terminate Deno process");
          throw;
        }
      }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      _logger?.LogError(ex, "Error occurred while stopping Deno process");
      throw;
    }
    finally
    {
      process.Dispose();
    }
  }

  /// <summary>
  /// Waits for the Deno process to exit.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous wait operation. The task result is the exit code.</returns>
  /// <exception cref="InvalidOperationException">Thrown if the process is not running.</exception>
  public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    Process? process;
    using (_lock.EnterScope())
    {
      process = _process;
    }

    if (process == null)
      throw new InvalidOperationException("Process is not running.");

    await process.WaitForExitAsync(cancellationToken);
    return process.ExitCode;
  }

  /// <summary>
  /// Restarts the Deno process by stopping it and starting it again.
  /// </summary>
  /// <param name="timeout">Maximum time to wait for the process to stop before forcefully killing it.</param>
  /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous restart operation.</returns>
  public async Task RestartAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    _logger?.LogInformation("Restarting Deno process");

    await StopAsync(timeout, cancellationToken);
    await StartAsync(cancellationToken);

    _logger?.LogInformation("Deno process restarted successfully");
  }

  /// <summary>
  /// Releases all resources used by the <see cref="DenoProcess"/>.
  /// </summary>
  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;

    Process? process;
    using (_lock.EnterScope())
    {
      process = _process;
      _process = null;
    }

    // Force kill the process immediately during disposal to avoid deadlocks
    if (process != null)
    {
      try
      {
        if (!process.HasExited)
        {
          process.Kill(entireProcessTree: true);
          // Give it a brief moment to clean up, but don't wait indefinitely
          if (!process.WaitForExit(1000))
          {
            _logger?.LogWarning("Process did not exit within 1 second during disposal");
          }
        }
        process.Dispose();
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Error occurred while disposing DenoProcess");
      }
    }

    // Clean up IPC transport
    try
    {
      _ipcTransport?.Dispose();
    }
    catch (Exception ex)
    {
      _logger?.LogError(ex, "Error occurred while disposing IPC transport");
    }

    // Clean up temporary config file if it exists
    if (!string.IsNullOrEmpty(_tempConfigPath))
    {
      try
      {
        Helper.DeleteTempFile(_tempConfigPath);
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Error occurred while cleaning up temporary config file: {ConfigPath}", _tempConfigPath);
      }
    }

    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Initializes the IPC transport and starts the server.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the operation.</param>
  private async Task InitializeIpcTransportAsync(CancellationToken cancellationToken)
  {
    if (_ipcTransportType == null || _ipcEndpointName == null)
      return;

    try
    {
      _logger?.LogInformation("Initializing IPC transport: {TransportType} at {Endpoint}", _ipcTransportType, _ipcEndpointName);

      _ipcTransport = IpcTransportFactory.CreateServer(_ipcEndpointName, _ipcTransportType.Value, _logger);

      // Set up event handlers
      _ipcTransport.MessageReceived += OnIpcMessageReceived;
      _ipcTransport.Connected += OnIpcConnected;
      _ipcTransport.Disconnected += OnIpcDisconnected;

      // Start the server
      await _ipcTransport.StartServerAsync(cancellationToken);

      _logger?.LogInformation("IPC transport server started successfully");
    }
    catch (Exception ex)
    {
      _logger?.LogError(ex, "Failed to initialize IPC transport");
      _ipcTransport?.Dispose();
      _ipcTransport = null;
      throw;
    }
  }

  /// <summary>
  /// Handles IPC message received events.
  /// </summary>
  /// <param name="sender">The transport that received the message.</param>
  /// <param name="e">Event arguments containing the message.</param>
  private void OnIpcMessageReceived(object? sender, IpcMessageReceivedEventArgs e)
  {
    try
    {
      _logger?.LogDebug("Received IPC message: {Message}", e.Message);
      // For now, just log the message. In the future, this could parse structured messages
      // when the structured communication feature is implemented.
    }
    catch (Exception ex)
    {
      _logger?.LogError(ex, "Error processing IPC message");
    }
  }

  /// <summary>
  /// Handles IPC connection events.
  /// </summary>
  /// <param name="sender">The transport that connected.</param>
  /// <param name="e">Event arguments.</param>
  private void OnIpcConnected(object? sender, EventArgs e)
  {
    _logger?.LogInformation("Deno process connected via IPC");
  }

  /// <summary>
  /// Handles IPC disconnection events.
  /// </summary>
  /// <param name="sender">The transport that disconnected.</param>
  /// <param name="e">Event arguments containing disconnection details.</param>
  private void OnIpcDisconnected(object? sender, IpcDisconnectedEventArgs e)
  {
    _logger?.LogInformation("Deno process disconnected from IPC: {Reason}", e.Reason);
    if (e.Exception != null)
    {
      _logger?.LogError(e.Exception, "IPC disconnection due to error");
    }
  }

  /// <summary>
  /// Calls a method on the Deno process and waits for the response.
  /// </summary>
  /// <param name="methodName">The name of the method to call.</param>
  /// <param name="parameters">The parameters to pass to the method.</param>
  /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous call operation. The task result contains the method response.</returns>
  /// <exception cref="InvalidOperationException">Thrown if the process is not running.</exception>
  public async Task<JsonElement> CallMethodAsync(string methodName, JsonElement? parameters = null, CancellationToken cancellationToken = default)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (!IsRunning)
      throw new InvalidOperationException("Process is not running.");

    var requestId = Interlocked.Increment(ref _nextRequestId).ToString();
    var tcs = new TaskCompletionSource<JsonElement>();

    _pendingRequests[requestId] = tcs;

    try
    {
      var request = new JsonRpcRequest
      {
        JsonRpc = "2.0",
        Id = requestId,
        Method = methodName,
        Params = parameters
      };

      var json = JsonSerializer.Serialize(request, JsonSerializerOptions.Default);
      await SendInputAsync(json, cancellationToken);

      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      linkedCts.CancelAfter(TimeSpan.FromSeconds(30)); // Default timeout

      return await tcs.Task.WaitAsync(linkedCts.Token);
    }
    finally
    {
      _pendingRequests.TryRemove(requestId, out _);
    }
  }

  /// <summary>
  /// Registers a method that can be called from the Deno process.
  /// </summary>
  /// <param name="methodName">The name of the method to register.</param>
  /// <param name="handler">The handler function that will be called when the method is invoked.</param>
  public void RegisterMethod(string methodName, Func<JsonElement, Task<JsonElement>> handler)
  {
    ArgumentNullException.ThrowIfNull(handler);

    _registeredMethods[methodName] = handler;
    _logger?.LogDebug("Registered method: {MethodName}", methodName);
  }

  /// <summary>
  /// Sends a notification to the Deno process (fire-and-forget).
  /// </summary>
  /// <param name="methodName">The name of the method to notify.</param>
  /// <param name="parameters">The parameters to pass to the method.</param>
  /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous send operation.</returns>
  /// <exception cref="InvalidOperationException">Thrown if the process is not running.</exception>
  public async Task SendNotificationAsync(string methodName, JsonElement? parameters = null, CancellationToken cancellationToken = default)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (!IsRunning)
      throw new InvalidOperationException("Process is not running.");

    var notification = new JsonRpcNotification
    {
      JsonRpc = "2.0",
      Method = methodName,
      Params = parameters
    };

    var json = JsonSerializer.Serialize(notification, JsonSerializerOptions.Default);
    await SendInputAsync(json, cancellationToken);
  }

  /// <summary>
  /// Finalizer for <see cref="DenoProcess"/>.
  /// </summary>
  ~DenoProcess()
  {
    Dispose();
  }
}

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

/// <summary>
/// Provides data for the StructuredMessageReceived event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StructuredMessageEventArgs"/> class.
/// </remarks>
/// <param name="message">The structured message that was received.</param>
public class StructuredMessageEventArgs(JsonRpcMessage message) : EventArgs
{
  /// <summary>
  /// Gets the structured message that was received.
  /// </summary>
  public JsonRpcMessage Message { get; } = message;
}

/// <summary>
/// Base class for JSON-RPC messages.
/// </summary>
public abstract class JsonRpcMessage
{
  /// <summary>
  /// Gets or sets the JSON-RPC version (always "2.0").
  /// </summary>
  public string JsonRpc { get; set; } = "2.0";

  /// <summary>
  /// Gets or sets the message ID (optional for notifications).
  /// </summary>
  public string? Id { get; set; }
}

/// <summary>
/// Represents a JSON-RPC request.
/// </summary>
public class JsonRpcRequest : JsonRpcMessage
{
  /// <summary>
  /// Gets or sets the method name to call.
  /// </summary>
  public string Method { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the method parameters.
  /// </summary>
  public JsonElement? Params { get; set; }
}

/// <summary>
/// Represents a JSON-RPC response.
/// </summary>
public class JsonRpcResponse : JsonRpcMessage
{
  /// <summary>
  /// Gets or sets the result (for successful responses).
  /// </summary>
  public JsonElement? Result { get; set; }

  /// <summary>
  /// Gets or sets the error (for error responses).
  /// </summary>
  public JsonRpcError? Error { get; set; }
}

/// <summary>
/// Represents a JSON-RPC notification (request without response).
/// </summary>
public class JsonRpcNotification : JsonRpcMessage
{
  /// <summary>
  /// Gets or sets the method name to call.
  /// </summary>
  public string Method { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the method parameters.
  /// </summary>
  public JsonElement? Params { get; set; }
}

/// <summary>
/// Represents a JSON-RPC error.
/// </summary>
public class JsonRpcError
{
  /// <summary>
  /// Gets or sets the error code.
  /// </summary>
  public int Code { get; set; }

  /// <summary>
  /// Gets or sets the error message.
  /// </summary>
  public string Message { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets additional error data.
  /// </summary>
  public JsonElement? Data { get; set; }
}

/// <summary>
/// Exception thrown when a JSON-RPC error occurs.
/// </summary>
public class JsonRpcException : Exception
{
  /// <summary>
  /// Gets the JSON-RPC error code.
  /// </summary>
  public int Code { get; }

  /// <summary>
  /// Gets additional error data.
  /// </summary>
  public new JsonElement? Data { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="JsonRpcException"/> class.
  /// </summary>
  /// <param name="code">The error code.</param>
  /// <param name="message">The error message.</param>
  /// <param name="data">Additional error data.</param>
  public JsonRpcException(int code, string message, JsonElement? data = null) : base(message)
  {
    Code = code;
    Data = data;
  }
}
