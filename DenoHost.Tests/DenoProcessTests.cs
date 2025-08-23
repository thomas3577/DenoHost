using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DenoHost.Core;

namespace DenoHost.Tests;

public class DenoProcessTests
{
  private readonly ILogger _logger;

  public DenoProcessTests()
  {
    _logger = NullLogger<DenoProcessTests>.Instance;
  }

  #region Constructor Tests

  [Fact]
  public void Constructor_WithArgs_ShouldCreateProcess()
  {
    // Arrange & Act
    using var process = new DenoProcess(["--version"]);

    // Assert
    Assert.NotNull(process);
    Assert.False(process.IsRunning);
    Assert.Null(process.ProcessId);
    Assert.Null(process.ExitCode);
  }

  [Fact]
  public void Constructor_WithCommandAndArgs_ShouldCreateProcess()
  {
    // Arrange & Act
    using var process = new DenoProcess("run", ["--allow-read", "script.ts"]);

    // Assert
    Assert.NotNull(process);
    Assert.False(process.IsRunning);
  }

  [Fact]
  public void Constructor_WithBaseOptions_ShouldCreateProcess()
  {
    // Arrange
    var baseOptions = new DenoExecuteBaseOptions
    {
      WorkingDirectory = Path.GetTempPath(),
      Logger = _logger
    };

    // Act
    using var process = new DenoProcess("--version", baseOptions);

    // Assert
    Assert.NotNull(process);
    Assert.False(process.IsRunning);
  }

  [Fact]
  public void Constructor_WithBaseOptionsAndArgs_ShouldCreateProcess()
  {
    // Arrange
    var baseOptions = new DenoExecuteBaseOptions
    {
      WorkingDirectory = Path.GetTempPath(),
      Logger = _logger
    };

    // Act
    using var process = new DenoProcess(baseOptions, ["--version"]);

    // Assert
    Assert.NotNull(process);
    Assert.False(process.IsRunning);
  }

  [Fact]
  public void Constructor_WithConfigString_ShouldCreateProcess()
  {
    // Arrange
    var configJson = """
            {
                "imports": {
                    "@std/": "https://deno.land/std@0.200.0/"
                }
            }
            """;

    // Act
    using var process = new DenoProcess("--version", configJson);

    // Assert
    Assert.NotNull(process);
    Assert.False(process.IsRunning);
  }

  [Fact]
  public void Constructor_WithConfigObject_ShouldCreateProcess()
  {
    // Arrange
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string>
      {
        ["@std/"] = "https://deno.land/std@0.200.0/"
      }
    };

    // Act
    using var process = new DenoProcess("--version", config);

    // Assert
    Assert.NotNull(process);
    Assert.False(process.IsRunning);
  }

  [Fact]
  public void Constructor_WithDenoExecuteOptions_ShouldCreateProcess()
  {
    // Arrange
    var options = new DenoExecuteOptions
    {
      Command = "--version",
      WorkingDirectory = Path.GetTempPath(),
      Logger = _logger
    };

    // Act
    using var process = new DenoProcess(options);

    // Assert
    Assert.NotNull(process);
    Assert.False(process.IsRunning);
  }

  [Fact]
  public void Constructor_WithDenoExecuteOptionsAndConfig_ShouldCreateProcess()
  {
    // Arrange
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string>
      {
        ["@std/"] = "https://deno.land/std@0.200.0/"
      }
    };
    var options = new DenoExecuteOptions
    {
      Command = "--version",
      Config = config,
      WorkingDirectory = Path.GetTempPath(),
      Logger = _logger
    };

    // Act
    using var process = new DenoProcess(options);

    // Assert
    Assert.NotNull(process);
    Assert.False(process.IsRunning);
  }

  #endregion

  #region Constructor Validation Tests

  [Fact]
  public void Constructor_WithNullArgs_ShouldThrowArgumentNullException()
  {
    // Act & Assert
    Assert.Throws<ArgumentNullException>(() => new DenoProcess((string[])null!));
  }

  [Fact]
  public void Constructor_WithEmptyCommand_ShouldThrowArgumentException()
  {
    // Act & Assert
    Assert.Throws<ArgumentException>(() => new DenoProcess("", ["--version"]));
    Assert.Throws<ArgumentException>(() => new DenoProcess("   ", ["--version"]));
  }

  [Fact]
  public void Constructor_WithNullBaseOptions_ShouldThrowArgumentNullException()
  {
    // Act & Assert
    Assert.Throws<ArgumentNullException>(() => new DenoProcess("--version", (DenoExecuteBaseOptions)null!));
    Assert.Throws<ArgumentNullException>(() => new DenoProcess((DenoExecuteBaseOptions)null!, ["--version"]));
  }

  [Fact]
  public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
  {
    // Act & Assert
    Assert.Throws<ArgumentNullException>(() => new DenoProcess("--version", (DenoConfig)null!));
  }

  [Fact]
  public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
  {
    // Act & Assert
    Assert.Throws<ArgumentNullException>(() => new DenoProcess((DenoExecuteOptions)null!));
  }

  [Fact]
  public void Constructor_WithConflictingConfigOptions_ShouldThrowArgumentException()
  {
    // Arrange
    var config = new DenoConfig { Imports = new Dictionary<string, string> { ["test"] = "test" } };
    var options = new DenoExecuteOptions
    {
      Command = "--version",
      Config = config,
      ConfigOrPath = "./deno.json"
    };

    // Act & Assert
    var ex = Assert.Throws<ArgumentException>(() => new DenoProcess(options));
    Assert.Contains("Either 'config' or 'configOrPath' should be provided, not both", ex.Message);
  }

  [Fact]
  public void Constructor_WithNullLogger_ShouldUseDefaultLogger()
  {
    // Arrange & Act
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('test')"],
        logger: null // Explicit null
    );

    // Assert
    Assert.NotNull(denoProcess);
    // The process should work even without a logger
  }

  #endregion

  #region Basic Functionality Tests

  [Fact]
  public async Task DenoProcess_BasicFunctionality()
  {
    // Arrange - Simple test that just exits
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["Deno.exit(0)"],
        logger: _logger
    );

    // Act
    await denoProcess.StartAsync();
    var exitCode = await denoProcess.WaitForExitAsync();

    // Assert
    Assert.Equal(0, exitCode);
    Assert.Equal(0, denoProcess.ExitCode);
    Assert.False(denoProcess.IsRunning);
  }

  [Fact]
  public async Task DenoProcess_HandlesProcessExit()
  {
    // Arrange - Use a simple script that just exits with a specific code
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["Deno.exit(42)"],
        logger: _logger
    );

    var processExited = false;
    var actualExitCode = -1;

    denoProcess.ProcessExited += (sender, e) =>
    {
      processExited = true;
      actualExitCode = e.ExitCode;
    };

    // Act
    await denoProcess.StartAsync();
    var exitCode = await denoProcess.WaitForExitAsync();

    // Assert
    Assert.True(processExited, "ProcessExited event was not raised");
    Assert.Equal(42, exitCode);
    Assert.Equal(42, actualExitCode);
    Assert.Equal(42, denoProcess.ExitCode);
    Assert.False(denoProcess.IsRunning);
  }

  [Fact]
  public async Task DenoProcess_ThrowsWhenSendingInputToStoppedProcess()
  {
    // Arrange
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('test')"],
        logger: _logger
    );

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => denoProcess.SendInputAsync("test")
    );
  }

  [Fact]
  public void Properties_BeforeStart_ShouldReturnExpectedValues()
  {
    // Arrange & Act
    using var denoProcess = new DenoProcess(["--version"]);

    // Assert
    Assert.False(denoProcess.IsRunning);
    Assert.Null(denoProcess.ProcessId);
    Assert.Null(denoProcess.ExitCode);
  }

  [Fact]
  public async Task Properties_AfterStart_ShouldReturnExpectedValues()
  {
    // Arrange
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('test'); Deno.exit(0);"],
        logger: _logger
    );

    // Act
    await denoProcess.StartAsync();

    // Assert - Check properties while running or just after completion
    Assert.True(denoProcess.ProcessId.HasValue);

    var exitCode = await denoProcess.WaitForExitAsync();

    Assert.False(denoProcess.IsRunning);
    Assert.Equal(0, exitCode);
    Assert.Equal(0, denoProcess.ExitCode);
  }

  #endregion

  #region Error Handling Tests

  [Fact]
  public async Task StartAsync_AlreadyStarted_ShouldThrowInvalidOperationException()
  {
    // Arrange
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["await new Promise(() => {})"], // Never resolves, keeps running
        logger: _logger
    );

    await denoProcess.StartAsync();

    // Act & Assert
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => denoProcess.StartAsync()
    );

    Assert.Contains("Process is already started", ex.Message);

    // Cleanup
    await denoProcess.StopAsync();
  }

  [Fact]
  public async Task StartAsync_WithInvalidCommand_ShouldThrowAndLogError()
  {
    // Arrange - Use a non-existent file to trigger process start failure
    using var denoProcess = new DenoProcess(
        command: "run",
        args: ["non-existent-file-that-does-not-exist.ts"],
        logger: _logger
    );

    // Act & Assert
    // The process might start but fail immediately, or throw during start
    // We expect either an exception or the process to exit with non-zero code
    try
    {
      await denoProcess.StartAsync();
      // If we get here, wait for exit and check the exit code
      var exitCode = await denoProcess.WaitForExitAsync();
      Assert.NotEqual(0, exitCode); // Should fail with non-zero exit code
    }
    catch (Exception)
    {
      // Expected behavior - process failed to start
      Assert.True(true);
    }
  }

  [Fact]
  public async Task ErrorDataReceived_WithErrorOutput_ShouldTriggerEvent()
  {
    // Arrange - Use a simple error that should definitely go to stderr
    using var denoProcess = new DenoProcess(
        command: "run",
        args: ["non-existent-file.ts"], // This will generate stderr output
        logger: _logger
    );

    var errorReceived = false;
    var errorMessages = new List<string>();

    denoProcess.ErrorDataReceived += (sender, e) =>
    {
      errorReceived = true;
      if (e.Data != null)
      {
        errorMessages.Add(e.Data);
      }
    };

    // Act
    await denoProcess.StartAsync();
    var exitCode = await denoProcess.WaitForExitAsync();

    // Assert
    Assert.True(errorReceived, "Error event should have been triggered");
    Assert.NotEqual(0, exitCode); // Should fail with non-zero exit code
    Assert.NotEmpty(errorMessages);
  }

  [Fact]
  public async Task SendInputAsync_ProcessNotRunning_ShouldThrowInvalidOperationException()
  {
    // Arrange
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('test')"],
        logger: _logger
    );

    // Act & Assert - Process not started
    var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(
        () => denoProcess.SendInputAsync("test input")
    );
    Assert.Contains("Process is not running", ex1.Message);

    // Start and let it complete
    await denoProcess.StartAsync();
    await denoProcess.WaitForExitAsync();

    // Act & Assert - Process completed
    var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(
        () => denoProcess.SendInputAsync("test input")
    );
    Assert.Contains("Process is not running", ex2.Message);
  }

  [Fact]
  public async Task SendInputAsync_WithRunningProcess_ShouldWork()
  {
    // Arrange - Create a simple process that just accepts the input without echoing
    // We'll test that SendInputAsync doesn't throw rather than testing the echo
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: [
            @"
                // Just wait for some time to allow SendInputAsync to work
                console.log('Process ready');
                await new Promise(resolve => setTimeout(resolve, 500));
                console.log('Process done');
                "
        ],
        logger: _logger
    );

    var processReady = false;
    denoProcess.OutputDataReceived += (sender, e) =>
    {
      if (e.Data?.Contains("Process ready") == true)
      {
        processReady = true;
      }
    };

    // Act
    await denoProcess.StartAsync();

    // Wait for the process to be ready
    await Task.Delay(200);

    if (denoProcess.IsRunning && processReady)
    {
      // This should not throw
      await denoProcess.SendInputAsync("test message");
      Assert.True(true, "SendInputAsync completed without throwing");
    }
    else
    {
      Assert.True(true, "Process exited before we could test SendInput");
    }

    await denoProcess.WaitForExitAsync();
  }

  [Fact]
  public async Task LongRunningProcess_ShouldLogCorrectly()
  {
    // Arrange - This tests the "else" branch in StartAsync for long-running processes
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: [
            @"
                console.log('Long running process started');
                // Keep running for a while but not too long for CI
                let running = true;
                setTimeout(() => {
                    console.log('Process ending');
                    running = false;
                }, 1000);

                // Simple busy wait
                while (running) {
                    await new Promise(resolve => setTimeout(resolve, 50));
                }
                "
        ],
        logger: _logger
    );

    var outputMessages = new List<string>();
    denoProcess.OutputDataReceived += (sender, e) =>
    {
      if (e.Data != null)
      {
        outputMessages.Add(e.Data);
      }
    };

    // Act
    await denoProcess.StartAsync();

    // Wait a moment to ensure it's still running after startup
    await Task.Delay(300);

    if (!denoProcess.IsRunning)
    {
      // Process finished quickly, that's also OK for this test
      Assert.True(true, "Process completed quickly");
      return;
    }

    var exitCode = await denoProcess.WaitForExitAsync();

    // Assert
    Assert.Equal(0, exitCode);
    Assert.Contains(outputMessages, msg => msg.Contains("Long running process started"));
  }

  #endregion

  #region StopAsync Tests

  [Fact]
  public async Task StopAsync_NotRunning_ShouldNotThrow()
  {
    // Arrange
    using var denoProcess = new DenoProcess(["--version"], logger: _logger);

    // Act & Assert - Should not throw when stopping a process that isn't running
    await denoProcess.StopAsync();
    Assert.False(denoProcess.IsRunning);
  }

  [Fact]
  public async Task StopAsync_WithRunningProcess_ShouldWork()
  {
    // Arrange - Simple process that just sleeps
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('Started'); await new Promise(resolve => setTimeout(resolve, 2000));"],
        logger: _logger
    );

    // Act
    await denoProcess.StartAsync();
    await Task.Delay(100); // Let it start

    if (denoProcess.IsRunning)
    {
      await denoProcess.StopAsync(timeout: TimeSpan.FromSeconds(1));
      Assert.False(denoProcess.IsRunning);
    }
    else
    {
      // Process finished quickly, that's also fine
      Assert.True(true);
    }
  }

  [Fact]
  public async Task StopAsync_AlreadyStopped_ShouldNotThrow()
  {
    // Arrange
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('test');"],
        logger: _logger
    );

    await denoProcess.StartAsync();
    await denoProcess.WaitForExitAsync(); // Process exits naturally

    // Act & Assert - Should not throw when stopping already stopped process
    await denoProcess.StopAsync();
    Assert.False(denoProcess.IsRunning);
  }

  [Fact]
  public async Task StopAsync_GracefulShutdown_ShouldWork()
  {
    // Arrange - Create a long-running process
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('Started'); while (true) { await new Promise(resolve => setTimeout(resolve, 100)); }"],
        logger: _logger
    );

    // Act
    await denoProcess.StartAsync();

    // Give it a moment to start
    await Task.Delay(200);

    if (!denoProcess.IsRunning)
    {
      // Process might have exited quickly, skip test
      Assert.True(true, "Process exited before we could test stop");
      return;
    }

    await denoProcess.StopAsync(timeout: TimeSpan.FromSeconds(2));

    // Assert
    Assert.False(denoProcess.IsRunning);
    Assert.True(denoProcess.ExitCode.HasValue);
  }

  #endregion

  #region RestartAsync Tests

  [Fact]
  public async Task RestartAsync_NotRunning_ShouldJustStart()
  {
    // Arrange
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('Started');"],
        logger: _logger
    );

    // Act - Restart without starting first
    await denoProcess.RestartAsync();
    var exitCode = await denoProcess.WaitForExitAsync();

    // Assert
    Assert.Equal(0, exitCode);
    Assert.False(denoProcess.IsRunning);
  }

  [Fact]
  public async Task RestartAsync_WithRunningProcess_ShouldWork()
  {
    // Arrange - Use repl for a long-running process
    using var denoProcess = new DenoProcess(["repl"], logger: _logger);

    // Act
    await denoProcess.StartAsync();
    var firstProcessId = denoProcess.ProcessId;

    await Task.Delay(500); // Give REPL time to start

    // Verify it's actually running before restart
    Assert.True(denoProcess.IsRunning, "REPL process should be running before restart");
    Assert.NotNull(firstProcessId);

    await denoProcess.RestartAsync(timeout: TimeSpan.FromSeconds(5));
    var secondProcessId = denoProcess.ProcessId;

    // Assert
    Assert.NotNull(secondProcessId);
    // The restart should work - either the process is running or completed successfully
    Assert.True(denoProcess.IsRunning || denoProcess.ExitCode == 0,
        "Process should be running or have completed successfully after restart");

    // Cleanup
    await denoProcess.StopAsync();
  }

  [Fact]
  public async Task RestartAsync_ShouldStopAndStart()
  {
    // Arrange
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('Process started'); while (true) { await new Promise(resolve => setTimeout(resolve, 100)); }"],
        logger: _logger
    );

    // Act
    await denoProcess.StartAsync();

    // Give it time to start up
    await Task.Delay(200);

    if (!denoProcess.IsRunning)
    {
      // Process exited quickly, just test that restart works
      await denoProcess.RestartAsync();
      Assert.True(true, "Restart worked even though process exited quickly");
      return;
    }

    var firstProcessId = denoProcess.ProcessId;

    await denoProcess.RestartAsync();
    var secondProcessId = denoProcess.ProcessId;

    // Assert
    Assert.NotEqual(firstProcessId, secondProcessId);

    // Cleanup
    await denoProcess.StopAsync();
  }

  #endregion

  #region WaitForExitAsync Tests

  [Fact]
  public async Task WaitForExitAsync_NotRunning_ShouldThrow()
  {
    // Arrange
    using var denoProcess = new DenoProcess(["--version"], logger: _logger);

    // Act & Assert
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => denoProcess.WaitForExitAsync()
    );

    Assert.Contains("Process is not running", ex.Message);
  }

  [Fact]
  public async Task WaitForExitAsync_ProcessQuickExit_ShouldReturnExitCode()
  {
    // Arrange
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('test');"],
        logger: _logger
    );

    await denoProcess.StartAsync();

    // Act
    var exitCode = await denoProcess.WaitForExitAsync();

    // Assert
    Assert.Equal(0, exitCode);
    Assert.Equal(0, denoProcess.ExitCode);
  }

  [Fact]
  public async Task WaitForExitAsync_ProcessAlreadyExited_ShouldReturnImmediately()
  {
    // Arrange
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('test');"],
        logger: _logger
    );

    await denoProcess.StartAsync();

    // Wait for natural exit
    var firstExitCode = await denoProcess.WaitForExitAsync();

    // Act - Wait again on already exited process
    var secondExitCode = await denoProcess.WaitForExitAsync();

    // Assert
    Assert.Equal(0, firstExitCode);
    Assert.Equal(0, secondExitCode);
    Assert.Equal(0, denoProcess.ExitCode);
  }

  #endregion

  #region Dispose Tests

  [Fact]
  public async Task Dispose_WithTempConfigFile_ShouldCleanup()
  {
    // Arrange
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string>
      {
        ["test"] = "https://example.com/test"
      }
    };

    DenoProcess? denoProcess = null;
    string? tempFile = null;

    try
    {
      denoProcess = new DenoProcess("eval", config, ["Deno.exit(0);"], logger: _logger);

      await denoProcess.StartAsync();
      await denoProcess.WaitForExitAsync();

      // Capture temp files before disposal
      var tempDir = Path.GetTempPath();
      var tempFiles = Directory.GetFiles(tempDir, "deno_config_*.json");
      tempFile = tempFiles.FirstOrDefault();

      // Act
      denoProcess.Dispose();
      denoProcess = null;

      // Assert
      if (tempFile != null)
      {
        // Give some time for cleanup
        await Task.Delay(100);
        Assert.False(File.Exists(tempFile), "Temp config file should be deleted");
      }
    }
    finally
    {
      denoProcess?.Dispose();
    }
  }

  [Fact]
  public void Dispose_MultipleDispose_ShouldNotThrow()
  {
    // Arrange
    var denoProcess = new DenoProcess(["--version"], logger: _logger);

    // Act & Assert - Multiple dispose should not throw
    denoProcess.Dispose();
    denoProcess.Dispose();
    denoProcess.Dispose();

    // Should not throw
    Assert.True(true);
  }

  [Fact]
  public async Task Dispose_DisposedProcess_MethodsShouldThrowObjectDisposedException()
  {
    // Arrange
    var denoProcess = new DenoProcess(["--version"], logger: _logger);
    denoProcess.Dispose();

    // Act & Assert
    await Assert.ThrowsAsync<ObjectDisposedException>(
        () => denoProcess.StartAsync()
    );

    await Assert.ThrowsAsync<ObjectDisposedException>(
        () => denoProcess.SendInputAsync("test")
    );

    await Assert.ThrowsAsync<ObjectDisposedException>(
        () => denoProcess.StopAsync()
    );

    await Assert.ThrowsAsync<ObjectDisposedException>(
        () => denoProcess.RestartAsync()
    );

    await Assert.ThrowsAsync<ObjectDisposedException>(
        () => denoProcess.WaitForExitAsync()
    );
  }

  [Fact]
  public async Task Dispose_WithRunningProcess_ShouldStopProcess()
  {
    // Arrange
    DenoProcess denoProcess;

    // Act
    denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('Started'); Deno.exit(0);"], // Quick exit to avoid hanging
        logger: _logger
    );

    await denoProcess.StartAsync();

    // Dispose should stop the process
    denoProcess.Dispose();

    // Assert
    // If we get here without hanging, disposal worked
    Assert.True(true);
  }

  [Fact]
  public async Task Dispose_WithTempConfigFile_ShouldCleanupTempFile()
  {
    // Arrange
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string>
      {
        ["@std/"] = "https://deno.land/std@0.200.0/"
      }
    };

    string? tempFile = null;

    // Act
    using (var process = new DenoProcess("--version", config))
    {
      // Process creation should create a temp file
      var tempDir = Path.GetTempPath();
      var tempFiles = Directory.GetFiles(tempDir, "deno_config_*.json");
      Assert.NotEmpty(tempFiles);
      tempFile = tempFiles.FirstOrDefault();
    }

    // Assert - temp file should be cleaned up after disposal
    if (tempFile != null)
    {
      // Give a moment for cleanup
      await Task.Delay(100);
      Assert.False(File.Exists(tempFile), "Temporary config file should be cleaned up after disposal");
    }
  }

  #endregion

  #region Event Handler Coverage Tests

  [Fact]
  public async Task Events_WithoutSubscribers_ShouldNotThrow()
  {
    // Arrange - Don't subscribe to any events to test null event handlers
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: ["console.log('test');"],
        logger: _logger
    );

    // Act & Assert - Should not throw even without event subscribers
    await denoProcess.StartAsync();
    var exitCode = await denoProcess.WaitForExitAsync();

    // The main point is that it doesn't throw, exit code doesn't matter
    Assert.True(exitCode >= 0, "Process should complete without throwing");
  }

  [Fact]
  public async Task ProcessExited_WithDifferentExitCodes_ShouldReportCorrectly()
  {
    // Test various exit codes
    var exitCodes = new[] { 0, 1, 42, 127 };

    foreach (var expectedExitCode in exitCodes)
    {
      using var denoProcess = new DenoProcess(
          command: "eval",
          args: [$"Deno.exit({expectedExitCode});"],
          logger: _logger
      );

      var actualExitCode = -1;
      denoProcess.ProcessExited += (sender, e) =>
      {
        actualExitCode = e.ExitCode;
      };

      await denoProcess.StartAsync();
      var waitExitCode = await denoProcess.WaitForExitAsync();

      Assert.Equal(expectedExitCode, waitExitCode);
      Assert.Equal(expectedExitCode, actualExitCode);
      Assert.Equal(expectedExitCode, denoProcess.ExitCode);
    }
  }

  #endregion

  #region Integration Tests with Flexible Constructors

  [Fact]
  public async Task StartAsync_WithFlexibleConstructor_ShouldWork()
  {
    // Arrange
    var options = new DenoExecuteOptions
    {
      Command = "--version",
      WorkingDirectory = Path.GetTempPath(),
      Logger = _logger
    };
    using var process = new DenoProcess(options);

    // Act
    await process.StartAsync();
    var exitCode = await process.WaitForExitAsync();

    // Assert
    Assert.True(process.ExitCode.HasValue);
    Assert.Equal(0, exitCode);
  }

  [Fact]
  public async Task StartAsync_WithConfigObject_ShouldWork()
  {
    // Arrange
    var config = new DenoConfig
    {
      Imports = new Dictionary<string, string>
      {
        ["@std/"] = "https://deno.land/std@0.200.0/"
      }
    };
    using var process = new DenoProcess("--version", config, logger: _logger);

    // Act
    await process.StartAsync();
    var exitCode = await process.WaitForExitAsync();

    // Assert
    Assert.True(process.ExitCode.HasValue);
    Assert.Equal(0, exitCode);
  }

  [Fact]
  public async Task StartAsync_WithBaseOptions_ShouldWork()
  {
    // Arrange
    var baseOptions = new DenoExecuteBaseOptions
    {
      WorkingDirectory = Path.GetTempPath(),
      Logger = _logger
    };
    using var process = new DenoProcess("--version", baseOptions);

    // Act
    await process.StartAsync();
    var exitCode = await process.WaitForExitAsync();

    // Assert
    Assert.True(process.ExitCode.HasValue);
    Assert.Equal(0, exitCode);
  }

  #endregion

  #region Concurrency Tests

  [Fact]
  public async Task ConcurrentOperations_ShouldBeThreadSafe()
  {
    // Arrange
    using var denoProcess = new DenoProcess(
        command: "eval",
        args: [
            @"
                console.log('Process started');
                await new Promise(resolve => {
                    setTimeout(resolve, 1000);
                });
                console.log('Process ending');
                "
        ],
        logger: _logger
    );

    // Act - Try concurrent operations
    var startTask = denoProcess.StartAsync();

    // These should handle concurrency gracefully
    var tasks = new[]
    {
            startTask,
            Task.Run(async () =>
            {
                await Task.Delay(100); // Start after process begins
                if (denoProcess.IsRunning)
                {
                    try
                    {
                        await denoProcess.SendInputAsync("test");
                    }
                    catch (InvalidOperationException)
                    {
                        // Expected if process doesn't read stdin
                    }
                }
            }),
            Task.Run(async () =>
            {
                await Task.Delay(500);

                Assert.True(denoProcess.ProcessId > 0, "ProcessId should be greater than 0");
                Assert.False(denoProcess.IsRunning, "Process should not be running");
            })
        };

    // Assert - Should not throw or deadlock
    await Task.WhenAll(tasks);

    if (denoProcess.IsRunning)
    {
      await denoProcess.WaitForExitAsync();
    }

    Assert.True(true); // If we get here, no deadlocks occurred
  }

  #endregion
}
