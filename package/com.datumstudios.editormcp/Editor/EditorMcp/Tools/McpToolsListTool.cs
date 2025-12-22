using System.Collections.Generic;
using System.Linq;
using DatumStudios.EditorMCP.Registry;
using DatumStudios.EditorMCP.Schemas;

namespace DatumStudios.EditorMCP.Tools
{
    /// <summary>
    /// Tool: mcp.tools.list - Lists all available tools with their metadata.
    /// </summary>
    public class McpToolsListTool : IEditorMcpTool
    {
        private readonly ToolRegistry _toolRegistry;

        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the McpToolsListTool class.
        /// </summary>
        /// <param name="toolRegistry">The tool registry to query.</param>
        public McpToolsListTool(ToolRegistry toolRegistry)
        {
            _toolRegistry = toolRegistry;
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to list all available tools.
        /// </summary>
        /// <param name="request">The tool invocation request with optional filters.</param>
        /// <returns>List of tool summaries.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            string category = null;
            string tier = null;

            if (request.Arguments != null)
            {
                if (request.Arguments.TryGetValue("category", out var categoryObj) && categoryObj is string)
                {
                    category = (string)categoryObj;
                }

                if (request.Arguments.TryGetValue("tier", out var tierObj) && tierObj is string)
                {
                    tier = (string)tierObj;
                }
            }

            var tools = _toolRegistry.List(category, tier);
            // Sort for deterministic ordering
            var sortedTools = tools.OrderBy(t => t.Id).ToList();
            var toolSummaries = sortedTools.Select(tool => new Dictionary<string, object>
            {
                { "id", tool.Id },
                { "name", tool.Name },
                { "category", tool.Category ?? "" },
                { "safetyLevel", tool.SafetyLevel.ToString() },
                { "description", tool.Description ?? "" },
                { "tier", tool.Tier ?? "" }
            }).ToArray();

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = new Dictionary<string, object>
                {
                    { "tools", toolSummaries }
                }
            };

            return response;
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "mcp.tools.list",
                Name = "List Tools",
                Description = "Lists all available tools with their metadata, categories, and tier availability. Required for MCP client discovery.",
                Category = "mcp.platform",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>
                {
                    {
                        "category",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Optional category filter"
                        }
                    },
                    {
                        "tier",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Optional tier filter"
                        }
                    }
                },
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "tools",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "List of tool summaries",
                            Items = new ToolOutputSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, ToolOutputSchema>
                                {
                                    {
                                        "id",
                                        new ToolOutputSchema
                                        {
                                            Type = "string",
                                            Description = "Tool identifier"
                                        }
                                    },
                                    {
                                        "name",
                                        new ToolOutputSchema
                                        {
                                            Type = "string",
                                            Description = "Human-readable tool name"
                                        }
                                    },
                                    {
                                        "category",
                                        new ToolOutputSchema
                                        {
                                            Type = "string",
                                            Description = "Tool category"
                                        }
                                    },
                                    {
                                        "safetyLevel",
                                        new ToolOutputSchema
                                        {
                                            Type = "string",
                                            Description = "Safety level (e.g., 'ReadOnly')"
                                        }
                                    },
                                    {
                                        "description",
                                        new ToolOutputSchema
                                        {
                                            Type = "string",
                                            Description = "Short description of the tool"
                                        }
                                    },
                                    {
                                        "tier",
                                        new ToolOutputSchema
                                        {
                                            Type = "string",
                                            Description = "Tier that provides access to this tool"
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                Notes = "Read-only. Returns tool metadata only; does not execute any tools."
            };
        }
    }
}

