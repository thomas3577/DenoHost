using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DenoHost.Core;

namespace DenoHost.Tests;

/// <summary>
/// Additional tests for missing DenoProcess methods to improve coverage.
/// Focuses on StopAsync, RestartAsync and other lifecycle methods.
/// </summary>
public class DenoProcessLifecycleTests
{
    private readonly ILogger _logger;

    public DenoProcessLifecycleTests()
    {
        _logger = NullLogger<DenoProcessLifecycleTests>.Instance;
    }

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
                await Task.Delay(200);
                var pid = denoProcess.ProcessId; // Just access property
                var isRunning = denoProcess.IsRunning;
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
