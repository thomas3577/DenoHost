using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DenoHost.Core.IPC;

/// <summary>
/// Named Pipes implementation of IPC transport for Windows.
/// Uses Windows Named Pipes for high-performance inter-process communication.
/// </summary>
public class NamedPipeTransport : IIpcTransport
{
    private readonly string _pipeName;
    private readonly ILogger? _logger;
    private readonly bool _isServer;
    private NamedPipeServerStream? _serverStream;
    private NamedPipeClientStream? _clientStream;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsConnected
    {
        get
        {
            if (_isServer)
                return _serverStream?.IsConnected == true;
            else
                return _clientStream?.IsConnected == true;
        }
    }

    /// <inheritdoc />
    public IpcTransportType TransportType => IpcTransportType.NamedPipes;

    /// <inheritdoc />
    public string Address => $"\\\\.\\pipe\\{_pipeName}";

    /// <inheritdoc />
    public event EventHandler<IpcMessageReceivedEventArgs>? MessageReceived;

    /// <inheritdoc />
    public event EventHandler? Connected;

    /// <inheritdoc />
    public event EventHandler<IpcDisconnectedEventArgs>? Disconnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeTransport"/> class as a server.
    /// </summary>
    /// <param name="pipeName">The name of the named pipe.</param>
    /// <param name="logger">Optional logger for transport operations.</param>
    public NamedPipeTransport(string pipeName, ILogger? logger = null)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        _logger = logger;
        _isServer = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeTransport"/> class as a client.
    /// </summary>
    /// <param name="pipeName">The name of the named pipe.</param>
    /// <param name="serverName">The name of the server (use "." for local server).</param>
    /// <param name="logger">Optional logger for transport operations.</param>
    public NamedPipeTransport(string pipeName, string serverName, ILogger? logger = null)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        _logger = logger;
        _isServer = false;

        _clientStream = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    }

    /// <inheritdoc />
    public async Task StartServerAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_isServer)
            throw new InvalidOperationException("This transport is configured as a client, not a server.");

        if (_serverStream != null)
            throw new InvalidOperationException("Server is already started.");

        _logger?.LogInformation("Starting Named Pipe server: {PipeName}", _pipeName);

        _serverStream = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            1, // Max instances
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        try
        {
            await _serverStream.WaitForConnectionAsync(cancellationToken);
            _logger?.LogInformation("Client connected to Named Pipe: {PipeName}", _pipeName);

            Connected?.Invoke(this, EventArgs.Empty);
            StartMessageListener();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start Named Pipe server: {PipeName}", _pipeName);
            _serverStream?.Dispose();
            _serverStream = null;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isServer)
            throw new InvalidOperationException("This transport is configured as a server, not a client.");

        if (_clientStream == null)
            throw new InvalidOperationException("Client stream is not initialized.");

        if (_clientStream.IsConnected)
            throw new InvalidOperationException("Client is already connected.");

        _logger?.LogInformation("Connecting to Named Pipe: {PipeName}", _pipeName);

        try
        {
            await _clientStream.ConnectAsync(cancellationToken);
            _logger?.LogInformation("Connected to Named Pipe: {PipeName}", _pipeName);

            Connected?.Invoke(this, EventArgs.Empty);
            StartMessageListener();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to Named Pipe: {PipeName}", _pipeName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
            throw new InvalidOperationException("Transport is not connected.");

        var stream = _isServer ? (Stream)_serverStream! : _clientStream!;
        var messageBytes = Encoding.UTF8.GetBytes(message + "\n");

        try
        {
            await stream.WriteAsync(messageBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            _logger?.LogDebug("Sent message via Named Pipe: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send message via Named Pipe");
            await HandleDisconnection("Send failed", ex, false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        _logger?.LogInformation("Closing Named Pipe transport: {PipeName}", _pipeName);

        // Stop the message listener
        _listenerCts?.Cancel();
        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger?.LogWarning("Message listener did not stop within timeout");
            }
        }

        // Close streams
        if (_isServer)
        {
            if (_serverStream?.IsConnected == true)
            {
                try
                {
                    _serverStream.Disconnect();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error disconnecting Named Pipe server");
                }
            }
            _serverStream?.Dispose();
            _serverStream = null;
        }
        else
        {
            _clientStream?.Dispose();
            _clientStream = null;
        }

        await HandleDisconnection("Closed", null, true);
    }

    private void StartMessageListener()
    {
        _listenerCts = new CancellationTokenSource();
        _listenerTask = Task.Run(async () =>
        {
            var stream = _isServer ? (Stream)_serverStream! : _clientStream!;
            var buffer = new byte[4096];
            var messageBuilder = new StringBuilder();

            try
            {
                while (!_listenerCts.Token.IsCancellationRequested && IsConnected)
                {
                    var bytesRead = await stream.ReadAsync(buffer, _listenerCts.Token);
                    if (bytesRead == 0)
                    {
                        // Connection closed by remote
                        await HandleDisconnection("Remote closed connection", null, true);
                        break;
                    }

                    var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(text);

                    // Process complete messages (line-based protocol)
                    var content = messageBuilder.ToString();
                    var lines = content.Split('\n');

                    // Keep the last incomplete line in the buffer
                    messageBuilder.Clear();
                    if (lines.Length > 0 && !content.EndsWith('\n'))
                    {
                        messageBuilder.Append(lines[^1]);
                        lines = lines[..^1];
                    }

                    // Process complete lines
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var trimmedLine = line.Trim();
                            _logger?.LogDebug("Received message via Named Pipe: {Message}", trimmedLine);
                            MessageReceived?.Invoke(this, new IpcMessageReceivedEventArgs(trimmedLine));
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in Named Pipe message listener");
                await HandleDisconnection("Listener error", ex, false);
            }
        }, _listenerCts.Token);
    }

    private async Task HandleDisconnection(string reason, Exception? exception, bool isGraceful)
    {
        if (_disposed)
            return;

        _logger?.LogInformation("Named Pipe disconnected: {Reason}", reason);

        try
        {
            Disconnected?.Invoke(this, new IpcDisconnectedEventArgs(reason, exception, isGraceful));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in disconnection event handler");
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel the listener
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();

        // Dispose streams
        _serverStream?.Dispose();
        _clientStream?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer for <see cref="NamedPipeTransport"/>.
    /// </summary>
    ~NamedPipeTransport()
    {
        Dispose();
    }
}
