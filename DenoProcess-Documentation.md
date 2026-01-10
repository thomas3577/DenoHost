# DenoProcess Class

The `DenoProcess` class extends the existing "fire and forget" functionality of
the DenoHost project with the ability to start, control, and terminate
long-lived Deno processes.

## Overview

Unlike the static `Deno` class, which executes Deno commands and waits for their
completion, the `DenoProcess` class enables:

- **Long-lived Processes**: Starting and managing Deno processes that run for
  extended periods
- **Bidirectional Communication**: Sending inputs to the process and receiving
  outputs
- **Process Management**: Monitoring process status, restart, and controlled
  shutdown
- **Event-based Architecture**: Responding to process events such as output or
  termination

## Core Features

### Process Lifecycle

- `StartAsync()`: Starts the Deno process
- `StopAsync()`: Stops the process (graceful shutdown with timeout; defaults to 30 seconds)
- `RestartAsync()`: Restarts the process
- `WaitForExitAsync()`: Waits for the natural termination of the process

### Communication

- `SendInputAsync()`: Sends input to the process via stdin
- `OutputDataReceived` Event: Receives output from stdout
- `ErrorDataReceived` Event: Receives error messages from stderr

### Monitoring

- `IsRunning`: Property to check process status
- `ProcessId`: Process ID of the running process
- `ExitCode`: Exit code after termination
- `ProcessExited` Event: Notification when process terminates

## Usage Examples

### Simple Usage

```csharp
using var denoProcess = new DenoProcess(
  command: "run",
  args: new[] { "--allow-read", "my-script.ts" },
  workingDirectory: "/path/to/scripts",
  logger: logger
);

// Subscribe to events
denoProcess.OutputDataReceived += (sender, e) => {
  Console.WriteLine($"Output: {e.Data}");
};

denoProcess.ErrorDataReceived += (sender, e) => {
  Console.WriteLine($"Error: {e.Data}");
};

// Start process
await denoProcess.StartAsync();

// Send inputs (optional)
await denoProcess.SendInputAsync("some command");

// Wait or stop later
await Task.Delay(5000);
await denoProcess.StopAsync();
```

### Interactive Communication

```csharp
using var denoProcess = new DenoProcess("run", new[] { "interactive-script.ts" });

var responses = new List<string>();
denoProcess.OutputDataReceived += (sender, e) => {
  if (!string.IsNullOrEmpty(e.Data))
    responses.Add(e.Data);
};

await denoProcess.StartAsync();

// Send commands
await denoProcess.SendInputAsync("command1");
await Task.Delay(1000);
await denoProcess.SendInputAsync("command2");
await Task.Delay(1000);
await denoProcess.SendInputAsync("exit");

await denoProcess.WaitForExitAsync();
```

### Process Monitoring

```csharp
using var denoProcess = new DenoProcess("run", new[] { "long-running-service.ts" });

denoProcess.ProcessExited += (sender, e) => {
  Console.WriteLine($"Process exited with code: {e.ExitCode}");

  if (e.ExitCode != 0) {
    // Process terminated unexpectedly, restart?
  }
};

await denoProcess.StartAsync();

// Monitoring in separate task
_ = Task.Run(async () => {
  while (denoProcess.IsRunning) {
    Console.WriteLine($"Process {denoProcess.ProcessId} is still running");
    await Task.Delay(5000);
  }
});

// Main application continues...
```

## Constructors

### DenoProcess(string[] args, ...)

Creates a new instance with direct Deno arguments.

```csharp
var process = new DenoProcess(
  args: new[] { "run", "--allow-read", "script.ts" },
  workingDirectory: "/path/to/scripts",
  logger: logger
);
```

### DenoProcess(string command, string[] args, ...)

Creates a new instance with a Deno command and additional arguments.

```csharp
var process = new DenoProcess(
  command: "run",
  args: new[] { "--allow-read", "script.ts" },
  workingDirectory: "/path/to/scripts",
  logger: logger
);
```

## Error Handling

The `DenoProcess` class provides robust error handling:

- **Start Errors**: `InvalidOperationException` when the process cannot be
  started
- **Communication Errors**: Exceptions when sending inputs to stopped processes
- **Timeout Handling**: Graceful shutdown with configurable timeout, followed by
  forced termination

Note: `StopAsync()` uses a default timeout of 30 seconds if none is provided. For tests/CI, it is recommended to pass an explicit, shorter timeout.

```csharp
try {
  await denoProcess.StartAsync();
} catch (InvalidOperationException ex) {
  logger.LogError("Failed to start Deno process: {Message}", ex.Message);
}

try {
  await denoProcess.StopAsync(timeout: TimeSpan.FromSeconds(30));
} catch (Exception ex) {
  logger.LogError("Error during process shutdown: {Message}", ex.Message);
}
```

## Comparison: Deno vs. DenoProcess

| Feature         | Deno (static)            | DenoProcess                                  |
| --------------- | ------------------------ | -------------------------------------------- |
| Execution Model | Fire-and-forget          | Long-lived process                           |
| Communication   | One-way (Input → Output) | Bidirectional                                |
| Process Control | None                     | Full control                                 |
| Events          | None                     | OutputReceived, ErrorReceived, ProcessExited |
| Lifetime        | Until completion         | User-controlled                              |
| Usage           | Simple script execution  | Interactive/Service-like applications        |

## Use Cases

- **Development Servers**: Deno-based development servers with hot-reload
- **Interactive Tools**: REPLs or interactive command-line tools
- **Long-lived Services**: Background services or daemons
- **Stream Processing**: Continuous data processing
- **Development Tools**: Build watchers or test runners

The `DenoProcess` class extends the DenoHost project with powerful process
management functionality, making it possible to use Deno not only for simple
script execution but also for complex, interactive applications.
