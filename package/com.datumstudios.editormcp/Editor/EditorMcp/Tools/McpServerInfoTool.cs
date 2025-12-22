using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DatumStudios.EditorMCP.Registry;
using DatumStudios.EditorMCP.Schemas;

namespace DatumStudios.EditorMCP.Tools
{
    /// <summary>
    /// Tool: mcp.server.info - Returns server and environment information.
    /// </summary>
    public class McpServerInfoTool : IEditorMcpTool
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly string _serverVersion;

        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the McpServerInfoTool class.
        /// </summary>
        /// <param name="toolRegistry">The tool registry to query for enabled categories.</param>
        /// <param name="serverVersion">The EditorMCP server version.</param>
        public McpServerInfoTool(ToolRegistry toolRegistry, string serverVersion = "0.1.0")
        {
            _toolRegistry = toolRegistry;
            _serverVersion = serverVersion;
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to return server information.
        /// </summary>
        /// <param name="request">The tool invocation request (no arguments required).</param>
        /// <returns>Server information response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            var categorySet = new HashSet<string>();
            var registeredTools = _toolRegistry.List();

            foreach (var tool in registeredTools)
            {
                if (!string.IsNullOrEmpty(tool.Category) && !categorySet.Contains(tool.Category))
                {
                    categorySet.Add(tool.Category);
                }
            }

            // Sort for deterministic ordering
            var categories = categorySet.OrderBy(c => c).ToArray();

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = new Dictionary<string, object>
                {
                    { "serverVersion", _serverVersion },
                    { "unityVersion", Application.unityVersion },
                    { "platform", Application.platform.ToString() },
                    { "enabledToolCategories", categories },
                    { "tier", "core" }
                }
            };

            return response;
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "mcp.server.info",
                Name = "MCP Server Info",
                Description = "Returns server and environment information to verify the MCP bridge is operational and provide context for tool execution.",
                Category = "mcp.platform",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>(),
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "serverVersion",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "EditorMCP server version"
                        }
                    },
                    {
                        "unityVersion",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Unity Editor version"
                        }
                    },
                    {
                        "platform",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Platform the server is running on"
                        }
                    },
                    {
                        "enabledToolCategories",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "Enabled tool categories",
                            Items = new ToolOutputSchema
                            {
                                Type = "string"
                            }
                        }
                    },
                    {
                        "tier",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Current tier (always 'core' for v0.1)"
                        }
                    }
                },
                Notes = "Read-only. Returns metadata only; no project state is modified."
            };
        }
    }
}

