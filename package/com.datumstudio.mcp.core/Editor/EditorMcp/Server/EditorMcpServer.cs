using System;
using System.Linq;
using UnityEngine;
using DatumStudio.Mcp.Core.Editor.Registry;
using DatumStudio.Mcp.Core.Editor.Schemas;
using DatumStudio.Mcp.Core.Editor.Tools;
using DatumStudio.Mcp.Core.Editor.Transport;

namespace DatumStudio.Mcp.Core.Editor.Server
{
    /// <summary>
    /// Main entry point for the EditorMCP server. Manages server lifecycle, tool registry,
    /// and provides status information. This is an editor-only, deterministic, read-only
    /// implementation for Core v0.1.
    /// </summary>
    public class EditorMcpServer
    {
        private readonly ToolRegistry _toolRegistry;
        private TransportHost _transportHost;
        private bool _isRunning;
        private string _serverVersion = "0.1.0";

        /// <summary>
        /// Gets the tool registry instance.
        /// </summary>
        public ToolRegistry ToolRegistry => _toolRegistry;

        /// <summary>
        /// Gets whether the server is currently running.
        /// </summary>
        public bool IsRunning => _isRunning && (_transportHost?.IsRunning ?? false);

        /// <summary>
        /// Gets the server version.
        /// </summary>
        public string ServerVersion => _serverVersion;

        /// <summary>
        /// Gets the transport host instance (if running).
        /// </summary>
        public TransportHost TransportHost => _transportHost;

        /// <summary>
        /// Initializes a new instance of the EditorMcpServer class.
        /// </summary>
        public EditorMcpServer()
        {
            _toolRegistry = new ToolRegistry();
            _isRunning = false;
        }

        /// <summary>
        /// Starts the MCP server. Initializes the tool registry and starts the stdio transport.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when server is already running.</exception>
        public void Start()
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Server is already running.");
            }

            // Register core tools first
            RegisterCoreTools();

            // Create and start transport host
            _transportHost = new TransportHost(_toolRegistry, _serverVersion);
            _transportHost.Start();

            _isRunning = true;
        }

        /// <summary>
        /// Stops the MCP server. Stops the transport and clears the tool registry.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            // Stop transport first
            _transportHost?.Stop("Server stopped");
            _transportHost?.Dispose();
            _transportHost = null;

            _toolRegistry.Clear();
            _isRunning = false;
        }

        /// <summary>
        /// Gets server status information.
        /// </summary>
        /// <returns>Server status including version, Unity version, platform, and enabled categories.</returns>
        public ServerStatus GetStatus()
        {
            return new ServerStatus
            {
                ServerVersion = _serverVersion,
                UnityVersion = Application.unityVersion,
                Platform = Application.platform.ToString(),
                EnabledToolCategories = GetEnabledCategories(),
                Tier = "core",
                IsRunning = IsRunning,
                TransportStartedAt = _transportHost?.StartedAt
            };
        }

        /// <summary>
        /// Registers all core tools for v0.1.
        /// </summary>
        private void RegisterCoreTools()
        {
            CoreToolBootstrapper.RegisterCoreTools(_toolRegistry, _serverVersion);
        }

        /// <summary>
        /// Gets the list of enabled tool categories.
        /// </summary>
        /// <returns>Array of category names.</returns>
        private string[] GetEnabledCategories()
        {
            var categories = _toolRegistry.List()
                .Select(t => t.Category)
                .Distinct()
                .ToArray();

            return categories;
        }
    }

    /// <summary>
    /// Server status information.
    /// </summary>
    public class ServerStatus
    {
        /// <summary>
        /// EditorMCP server version.
        /// </summary>
        public string ServerVersion { get; set; }

        /// <summary>
        /// Unity Editor version.
        /// </summary>
        public string UnityVersion { get; set; }

        /// <summary>
        /// Platform the server is running on.
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// Enabled tool categories.
        /// </summary>
        public string[] EnabledToolCategories { get; set; }

        /// <summary>
        /// Current tier (always "core" for v0.1).
        /// </summary>
        public string Tier { get; set; }

        /// <summary>
        /// Whether the server is currently running.
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// When the transport was started (if running).
        /// </summary>
        public DateTime? TransportStartedAt { get; set; }
    }
}

