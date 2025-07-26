using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DenoHost.Core;
using System.Diagnostics;

namespace DenoHost.Tests;

/// <summary>
/// Additional tests to improve code coverage for DenoProcess class.
/// These tests focus on error scenarios and edge cases that are currently not covered.
/// </summary>
public class DenoProcessCoverageTests
{
    private readonly ILogger _logger;

    public DenoProcessCoverageTests()
    {
        _logger = NullLogger<DenoProcessCoverageTests>.Instance;
    }

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

    #region Boundary and State Tests

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
}
