using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DenoHost.Core.IPC;

/// <summary>
/// Unix Domain Sockets implementation of IPC transport for Linux and macOS.
/// Uses Unix Domain Sockets for high-performance inter-process communication on Unix-like systems.
/// </summary>
public class UnixDomainSocketTransport : IIpcTransport
{
    private readonly string _socketPath;
    private readonly ILogger? _logger;
    private readonly bool _isServer;
    private Socket? _serverSocket;
    private Socket? _clientSocket;
    private Socket? _connectedSocket;
    private NetworkStream? _stream;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsConnected => _connectedSocket?.Connected == true;

    /// <inheritdoc />
    public IpcTransportType TransportType => IpcTransportType.UnixDomainSockets;

    /// <inheritdoc />
    public string Address => _socketPath;

    /// <inheritdoc />
    public event EventHandler<IpcMessageReceivedEventArgs>? MessageReceived;

    /// <inheritdoc />
    public event EventHandler? Connected;

    /// <inheritdoc />
    public event EventHandler<IpcDisconnectedEventArgs>? Disconnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnixDomainSocketTransport"/> class as a server.
    /// </summary>
    /// <param name="socketPath">The path to the Unix domain socket file.</param>
    /// <param name="logger">Optional logger for transport operations.</param>
    public UnixDomainSocketTransport(string socketPath, ILogger? logger = null)
    {
        _socketPath = socketPath ?? throw new ArgumentNullException(nameof(socketPath));
        _logger = logger;
        _isServer = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnixDomainSocketTransport"/> class as a client.
    /// </summary>
    /// <param name="socketPath">The path to the Unix domain socket file.</param>
    /// <param name="isClient">Must be true to indicate client mode.</param>
    /// <param name="logger">Optional logger for transport operations.</param>
    public UnixDomainSocketTransport(string socketPath, bool isClient, ILogger? logger = null)
    {
        _socketPath = socketPath ?? throw new ArgumentNullException(nameof(socketPath));
        _logger = logger;
        _isServer = !isClient;
    }

    /// <inheritdoc />
    public async Task StartServerAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_isServer)
            throw new InvalidOperationException("This transport is configured as a client, not a server.");

        if (_serverSocket != null)
            throw new InvalidOperationException("Server is already started.");

        _logger?.LogInformation("Starting Unix Domain Socket server: {SocketPath}", _socketPath);

        // Remove existing socket file if it exists
        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
            _logger?.LogDebug("Removed existing socket file: {SocketPath}", _socketPath);
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_socketPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            _serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endPoint = new UnixDomainSocketEndPoint(_socketPath);
            _serverSocket.Bind(endPoint);
            _serverSocket.Listen(1);

            _logger?.LogInformation("Unix Domain Socket server listening: {SocketPath}", _socketPath);

            // Accept connection asynchronously
            _connectedSocket = await _serverSocket.AcceptAsync(cancellationToken);
            _stream = new NetworkStream(_connectedSocket);

            _logger?.LogInformation("Client connected to Unix Domain Socket: {SocketPath}", _socketPath);

            Connected?.Invoke(this, EventArgs.Empty);
            StartMessageListener();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start Unix Domain Socket server: {SocketPath}", _socketPath);
            CleanupServer();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isServer)
            throw new InvalidOperationException("This transport is configured as a server, not a client.");

        if (_clientSocket != null)
            throw new InvalidOperationException("Client is already connected.");

        _logger?.LogInformation("Connecting to Unix Domain Socket: {SocketPath}", _socketPath);

        if (!File.Exists(_socketPath))
            throw new InvalidOperationException($"Socket file does not exist: {_socketPath}");

        try
        {
            _clientSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endPoint = new UnixDomainSocketEndPoint(_socketPath);

            await _clientSocket.ConnectAsync(endPoint, cancellationToken);
            _connectedSocket = _clientSocket;
            _stream = new NetworkStream(_connectedSocket);

            _logger?.LogInformation("Connected to Unix Domain Socket: {SocketPath}", _socketPath);

            Connected?.Invoke(this, EventArgs.Empty);
            StartMessageListener();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to Unix Domain Socket: {SocketPath}", _socketPath);
            _clientSocket?.Dispose();
            _clientSocket = null;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected || _stream == null)
            throw new InvalidOperationException("Transport is not connected.");

        var messageBytes = Encoding.UTF8.GetBytes(message + "\n");

        try
        {
            await _stream.WriteAsync(messageBytes, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
            _logger?.LogDebug("Sent message via Unix Domain Socket: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send message via Unix Domain Socket");
            await HandleDisconnection("Send failed", ex, false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        _logger?.LogInformation("Closing Unix Domain Socket transport: {SocketPath}", _socketPath);

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

        // Close connections
        _stream?.Dispose();
        _stream = null;

        _connectedSocket?.Dispose();
        _connectedSocket = null;

        if (_isServer)
        {
            CleanupServer();
        }
        else
        {
            _clientSocket?.Dispose();
            _clientSocket = null;
        }

        await HandleDisconnection("Closed", null, true);
    }

    private void CleanupServer()
    {
        _serverSocket?.Dispose();
        _serverSocket = null;

        // Remove socket file
        try
        {
            if (File.Exists(_socketPath))
            {
                File.Delete(_socketPath);
                _logger?.LogDebug("Removed socket file: {SocketPath}", _socketPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to remove socket file: {SocketPath}", _socketPath);
        }
    }

    private void StartMessageListener()
    {
        _listenerCts = new CancellationTokenSource();
        _listenerTask = Task.Run(async () =>
        {
            if (_stream == null)
                return;

            var buffer = new byte[4096];
            var messageBuilder = new StringBuilder();

            try
            {
                while (!_listenerCts.Token.IsCancellationRequested && IsConnected)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, _listenerCts.Token);
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
                            _logger?.LogDebug("Received message via Unix Domain Socket: {Message}", trimmedLine);
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
                _logger?.LogError(ex, "Error in Unix Domain Socket message listener");
                await HandleDisconnection("Listener error", ex, false);
            }
        }, _listenerCts.Token);
    }

    private async Task HandleDisconnection(string reason, Exception? exception, bool isGraceful)
    {
        if (_disposed)
            return;

        _logger?.LogInformation("Unix Domain Socket disconnected: {Reason}", reason);

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

        // Dispose connections
        _stream?.Dispose();
        _connectedSocket?.Dispose();
        _clientSocket?.Dispose();

        if (_isServer)
        {
            CleanupServer();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer for <see cref="UnixDomainSocketTransport"/>.
    /// </summary>
    ~UnixDomainSocketTransport()
    {
        Dispose();
    }
}
