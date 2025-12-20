using System;
using DatumStudio.Mcp.Core.Editor.Registry;

namespace DatumStudio.Mcp.Core.Editor.Transport
{
    /// <summary>
    /// Owns the transport lifecycle. Manages starting and stopping the stdio transport
    /// and provides status information about the transport state.
    /// </summary>
    public class TransportHost : IDisposable
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly string _serverVersion;
        private StdioTransport _transport;
        private McpMessageRouter _router;
        private bool _isRunning;
        private DateTime? _startedAt;
        private bool _disposed;

        /// <summary>
        /// Gets whether the transport is currently running.
        /// </summary>
        public bool IsRunning => _isRunning && !_disposed;

        /// <summary>
        /// Gets the time when the transport was started, or null if not started.
        /// </summary>
        public DateTime? StartedAt => _startedAt;

        /// <summary>
        /// Initializes a new instance of the TransportHost class.
        /// </summary>
        /// <param name="toolRegistry">The tool registry to use for routing requests.</param>
        /// <param name="serverVersion">The EditorMCP server version.</param>
        public TransportHost(ToolRegistry toolRegistry, string serverVersion)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _serverVersion = serverVersion ?? throw new ArgumentNullException(nameof(serverVersion));
        }

        /// <summary>
        /// Starts the transport. Initializes the router and begins reading from stdin.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when transport is already running.</exception>
        public void Start()
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Transport is already running.");
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TransportHost));
            }

            // Initialize router
            _router = new McpMessageRouter(_toolRegistry, _serverVersion);

            // Initialize transport
            _transport = new StdioTransport(_router);

            // Start transport
            _transport.Start();
            _isRunning = true;
            _startedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Stops the transport and releases resources.
        /// </summary>
        /// <param name="reason">Optional reason for stopping (for logging/debugging).</param>
        public void Stop(string reason = null)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _startedAt = null;

            _transport?.Stop();
            _transport?.Dispose();
            _transport = null;
            _router = null;

            if (!string.IsNullOrEmpty(reason))
            {
                UnityEngine.Debug.Log($"EditorMCP transport stopped: {reason}");
            }
        }

        /// <summary>
        /// Disposes the transport host and releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Stop("Disposing TransportHost");
                _disposed = true;
            }
        }
    }
}

