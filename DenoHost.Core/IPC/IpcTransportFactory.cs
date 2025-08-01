using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DenoHost.Core.IPC;

/// <summary>
/// Factory for creating IPC transport instances with automatic platform detection.
/// Selects the most appropriate transport mechanism based on the current operating system.
/// </summary>
public static class IpcTransportFactory
{
    /// <summary>
    /// Creates an IPC transport server using the best available transport for the current platform.
    /// </summary>
    /// <param name="name">The name/identifier for the IPC endpoint (pipe name or socket path).</param>
    /// <param name="logger">Optional logger for transport operations.</param>
    /// <returns>An IPC transport configured as a server.</returns>
    public static IIpcTransport CreateServer(string name, ILogger? logger = null)
    {
        return CreateServer(name, GetPreferredTransportType(), logger);
    }

    /// <summary>
    /// Creates an IPC transport server using the specified transport type.
    /// </summary>
    /// <param name="name">The name/identifier for the IPC endpoint.</param>
    /// <param name="transportType">The specific transport type to use.</param>
    /// <param name="logger">Optional logger for transport operations.</param>
    /// <returns>An IPC transport configured as a server.</returns>
    public static IIpcTransport CreateServer(string name, IpcTransportType transportType, ILogger? logger = null)
    {
        return transportType switch
        {
            IpcTransportType.NamedPipes => CreateNamedPipeServer(name, logger),
            IpcTransportType.UnixDomainSockets => CreateUnixDomainSocketServer(name, logger),
            IpcTransportType.TcpLoopback => CreateTcpLoopbackServer(name, logger),
            IpcTransportType.StandardStreams => CreateStandardStreamsTransport(logger),
            _ => throw new ArgumentException($"Unsupported transport type: {transportType}", nameof(transportType))
        };
    }

    /// <summary>
    /// Creates an IPC transport client using the best available transport for the current platform.
    /// </summary>
    /// <param name="name">The name/identifier for the IPC endpoint (pipe name or socket path).</param>
    /// <param name="logger">Optional logger for transport operations.</param>
    /// <returns>An IPC transport configured as a client.</returns>
    public static IIpcTransport CreateClient(string name, ILogger? logger = null)
    {
        return CreateClient(name, GetPreferredTransportType(), logger);
    }

    /// <summary>
    /// Creates an IPC transport client using the specified transport type.
    /// </summary>
    /// <param name="name">The name/identifier for the IPC endpoint.</param>
    /// <param name="transportType">The specific transport type to use.</param>
    /// <param name="logger">Optional logger for transport operations.</param>
    /// <returns>An IPC transport configured as a client.</returns>
    public static IIpcTransport CreateClient(string name, IpcTransportType transportType, ILogger? logger = null)
    {
        return transportType switch
        {
            IpcTransportType.NamedPipes => CreateNamedPipeClient(name, logger),
            IpcTransportType.UnixDomainSockets => CreateUnixDomainSocketClient(name, logger),
            IpcTransportType.TcpLoopback => CreateTcpLoopbackClient(name, logger),
            IpcTransportType.StandardStreams => CreateStandardStreamsTransport(logger),
            _ => throw new ArgumentException($"Unsupported transport type: {transportType}", nameof(transportType))
        };
    }

    /// <summary>
    /// Gets the preferred transport type for the current platform.
    /// </summary>
    /// <returns>The recommended transport type for optimal performance.</returns>
    public static IpcTransportType GetPreferredTransportType()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IpcTransportType.NamedPipes;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return IpcTransportType.UnixDomainSockets;
        }
        else
        {
            // Fallback for unknown platforms
            return IpcTransportType.TcpLoopback;
        }
    }

    /// <summary>
    /// Checks if the specified transport type is supported on the current platform.
    /// </summary>
    /// <param name="transportType">The transport type to check.</param>
    /// <returns>True if the transport is supported, false otherwise.</returns>
    public static bool IsTransportSupported(IpcTransportType transportType)
    {
        return transportType switch
        {
            IpcTransportType.NamedPipes => RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            IpcTransportType.UnixDomainSockets => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                                                  RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
            IpcTransportType.TcpLoopback => true, // Supported on all platforms
            IpcTransportType.StandardStreams => true, // Supported on all platforms
            _ => false
        };
    }

    /// <summary>
    /// Generates a unique IPC endpoint name with optional prefix.
    /// </summary>
    /// <param name="prefix">Optional prefix for the endpoint name.</param>
    /// <returns>A unique endpoint name suitable for the current platform.</returns>
    public static string GenerateUniqueEndpointName(string? prefix = null)
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var baseName = string.IsNullOrEmpty(prefix) ? $"denohost_{uniqueId}" : $"{prefix}_{uniqueId}";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Named pipe names
            return baseName;
        }
        else
        {
            // Unix domain socket paths
            var tempDir = Path.GetTempPath();
            return Path.Combine(tempDir, $"{baseName}.sock");
        }
    }

    private static IIpcTransport CreateNamedPipeServer(string pipeName, ILogger? logger)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Named Pipes are only supported on Windows.");

        return new NamedPipeTransport(pipeName, logger);
    }

    private static IIpcTransport CreateNamedPipeClient(string pipeName, ILogger? logger)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Named Pipes are only supported on Windows.");

        return new NamedPipeTransport(pipeName, ".", logger);
    }

    private static IIpcTransport CreateUnixDomainSocketServer(string socketPath, ILogger? logger)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("Unix Domain Sockets are only supported on Linux and macOS.");

        return new UnixDomainSocketTransport(socketPath, logger);
    }

    private static IIpcTransport CreateUnixDomainSocketClient(string socketPath, ILogger? logger)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("Unix Domain Sockets are only supported on Linux and macOS.");

        return new UnixDomainSocketTransport(socketPath, isClient: true, logger);
    }

    private static IIpcTransport CreateTcpLoopbackServer(string name, ILogger? logger)
    {
        // TODO: Implement TcpLoopbackTransport
        throw new NotImplementedException("TCP Loopback transport is not yet implemented.");
    }

    private static IIpcTransport CreateTcpLoopbackClient(string name, ILogger? logger)
    {
        // TODO: Implement TcpLoopbackTransport
        throw new NotImplementedException("TCP Loopback transport is not yet implemented.");
    }

    private static IIpcTransport CreateStandardStreamsTransport(ILogger? logger)
    {
        // TODO: Implement StandardStreamsTransport (legacy stdin/stdout support)
        throw new NotImplementedException("Standard Streams transport is not yet implemented.");
    }
}
