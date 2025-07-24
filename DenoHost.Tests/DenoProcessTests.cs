using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DenoHost.Core;

namespace DenoHost.Tests;

/// <summary>
/// Comprehensive tests for the DenoProcess class, including all constructor overloads and functionality.
/// </summary>
public class DenoProcessTests
{
  private readonly ILogger _logger;

  public DenoProcessTests()
  {
    // For simplicity, use NullLogger in tests
    // In a real test setup, you might want to use a proper test logger
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
}
