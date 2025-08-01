using System;
using System.Threading;
using System.Threading.Tasks;

namespace DenoHost.Core.IPC;

/// <summary>
/// Represents an inter-process communication transport layer.
/// Provides abstraction over different IPC mechanisms like Named Pipes, Unix Domain Sockets, etc.
/// </summary>
public interface IIpcTransport : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the transport is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the transport type for this instance.
    /// </summary>
    IpcTransportType TransportType { get; }

    /// <summary>
    /// Gets the connection address/path for this transport.
    /// </summary>
    string Address { get; }

    /// <summary>
    /// Event raised when a message is received from the remote endpoint.
    /// </summary>
    event EventHandler<IpcMessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Event raised when the connection is established.
    /// </summary>
    event EventHandler? Connected;

    /// <summary>
    /// Event raised when the connection is lost or closed.
    /// </summary>
    event EventHandler<IpcDisconnectedEventArgs>? Disconnected;

    /// <summary>
    /// Starts the transport server and begins listening for connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    Task StartServerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects to a remote IPC server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous connect operation.</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to the remote endpoint.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the connection and stops the transport.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous close operation.</returns>
    Task CloseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Specifies the type of IPC transport mechanism.
/// </summary>
public enum IpcTransportType
{
    /// <summary>
    /// Named Pipes (Windows preferred).
    /// </summary>
    NamedPipes,

    /// <summary>
    /// Unix Domain Sockets (Linux/Mac preferred).
    /// </summary>
    UnixDomainSockets,

    /// <summary>
    /// TCP Loopback (universal fallback).
    /// </summary>
    TcpLoopback,

    /// <summary>
    /// Standard input/output streams (legacy fallback).
    /// </summary>
    StandardStreams
}

/// <summary>
/// Provides data for the MessageReceived event.
/// </summary>
public class IpcMessageReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the received message content.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the timestamp when the message was received.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IpcMessageReceivedEventArgs"/> class.
    /// </summary>
    /// <param name="message">The received message content.</param>
    public IpcMessageReceivedEventArgs(string message)
    {
        Message = message;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Provides data for the Disconnected event.
/// </summary>
public class IpcDisconnectedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the reason for disconnection.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Gets the exception that caused the disconnection, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets a value indicating whether the disconnection was expected/graceful.
    /// </summary>
    public bool IsGraceful { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IpcDisconnectedEventArgs"/> class.
    /// </summary>
    /// <param name="reason">The reason for disconnection.</param>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    /// <param name="isGraceful">Whether the disconnection was expected/graceful.</param>
    public IpcDisconnectedEventArgs(string? reason = null, Exception? exception = null, bool isGraceful = true)
    {
        Reason = reason;
        Exception = exception;
        IsGraceful = isGraceful;
    }
}
