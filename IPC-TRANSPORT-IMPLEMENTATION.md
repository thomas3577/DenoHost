# IPC Transport Implementation

This document describes the IPC (Inter-Process Communication) transport
implementation for DenoHost.

## Overview

The IPC transport layer provides professional-grade communication channels
between .NET and Deno processes, replacing the unconventional stdin/stdout
approach with dedicated IPC mechanisms.

## Architecture

### Transport Types

1. **Named Pipes** (Windows)
   - Native Windows Named Pipes using `System.IO.Pipes`
   - High performance, low latency
   - Optimal for Windows environments

2. **Unix Domain Sockets** (Linux/macOS)
   - Native Unix Domain Sockets using `System.Net.Sockets`
   - High performance, low latency
   - Optimal for Unix-like systems

3. **TCP Loopback** (Cross-platform fallback)
   - TCP sockets over localhost
   - Universal compatibility
   - Slightly higher latency

4. **Standard Streams** (Legacy support)
   - stdin/stdout communication
   - Backward compatibility
   - Simple but unconventional

### Auto-Detection

The `IpcTransportFactory` automatically selects the best transport for the
current platform:

- **Windows**: Named Pipes
- **Linux/macOS**: Unix Domain Sockets
- **Other**: TCP Loopback

## Usage Examples

### Basic IPC with Auto-Detection

```csharp
// Create DenoProcess with auto-detected IPC transport
var process = new DenoProcess(
    command: "run",
    args: ["--allow-all", "my-script.ts"],
    ipcTransportType: IpcTransportFactory.GetPreferredTransportType()
);

await process.StartAsync();
```

### Explicit Transport Selection

```csharp
// Use Named Pipes explicitly (Windows only)
var process = new DenoProcess(
    command: "run",
    args: ["--allow-all", "my-script.ts"],
    ipcTransportType: IpcTransportType.NamedPipes,
    ipcEndpointName: "my-custom-pipe"
);

await process.StartAsync();
```

### Unix Domain Sockets (Linux/macOS)

```csharp
// Use Unix Domain Sockets explicitly
var process = new DenoProcess(
    command: "run",
    args: ["--allow-all", "my-script.ts"],
    ipcTransportType: IpcTransportType.UnixDomainSockets,
    ipcEndpointName: "/tmp/my-socket.sock"
);

await process.StartAsync();
```

## Implementation Details

### Transport Interface

All transports implement the `IIpcTransport` interface:

```csharp
public interface IIpcTransport : IDisposable
{
    bool IsConnected { get; }
    IpcTransportType TransportType { get; }
    string Address { get; }
    
    event EventHandler<IpcMessageReceivedEventArgs>? MessageReceived;
    event EventHandler? Connected;
    event EventHandler<IpcDisconnectedEventArgs>? Disconnected;
    
    Task StartServerAsync(CancellationToken cancellationToken = default);
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
}
```

### Message Protocol

All transports use a line-based message protocol:

- Messages are terminated with newline characters (`\n`)
- UTF-8 encoding is used
- JSON format for structured data

### Error Handling

- Automatic fallback to stdin/stdout if IPC initialization fails
- Comprehensive logging for debugging
- Graceful disconnection handling
- Resource cleanup on disposal

## Performance Characteristics

| Transport Type      | Latency  | Throughput | Platform Support |
| ------------------- | -------- | ---------- | ---------------- |
| Named Pipes         | Very Low | Very High  | Windows Only     |
| Unix Domain Sockets | Very Low | Very High  | Linux/macOS Only |
| TCP Loopback        | Low      | High       | Cross-platform   |
| Standard Streams    | Medium   | Medium     | Cross-platform   |

## Security Considerations

- Named Pipes and Unix Domain Sockets provide process isolation
- Local-only communication (no network exposure)
- Automatic cleanup of socket files
- Process lifetime-bound connections

## Future Enhancements

1. **Structured Communication Integration**
   - JSON-RPC 2.0 protocol over IPC transports
   - Method registration and invocation
   - Bidirectional API bridge

2. **Connection Pooling**
   - Multiple concurrent connections
   - Load balancing across processes

3. **Authentication**
   - Process identity verification
   - Access control mechanisms

4. **Encryption**
   - TLS encryption for TCP transports
   - Secure channel establishment

## Migration from stdin/stdout

Existing code using stdin/stdout will continue to work unchanged. To migrate to
IPC:

1. Add `ipcTransportType` parameter to `DenoProcess` constructor
2. Use auto-detection for seamless cross-platform operation
3. Test thoroughly on target platforms
4. Monitor performance improvements

## Troubleshooting

### Common Issues

1. **Permission Errors**
   - Ensure proper permissions for socket files
   - Check Named Pipe access rights

2. **Platform Compatibility**
   - Use auto-detection for maximum compatibility
   - Fallback to TCP Loopback for unknown platforms

3. **Connection Failures**
   - Check if endpoint names are unique
   - Verify no port conflicts for TCP Loopback
   - Review log output for detailed error information

### Logging

Enable debug logging to troubleshoot IPC issues:

```csharp
var logger = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug)).CreateLogger<DenoProcess>();

var process = new DenoProcess(args, logger: logger, ipcTransportType: IpcTransportType.NamedPipes);
```

## Conclusion

The IPC transport implementation provides a robust, high-performance foundation
for .NET-Deno communication. It replaces the unconventional stdin/stdout
approach with professional-grade IPC mechanisms while maintaining backward
compatibility and cross-platform support.
