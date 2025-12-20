using System.Collections.Generic;
using System.Linq;
using DatumStudio.Mcp.Core.Editor.Registry;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Tool: mcp.tool.describe - Returns the complete schema for a specific tool.
    /// </summary>
    public class McpToolDescribeTool : IEditorMcpTool
    {
        private readonly ToolRegistry _toolRegistry;

        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the McpToolDescribeTool class.
        /// </summary>
        /// <param name="toolRegistry">The tool registry to query.</param>
        public McpToolDescribeTool(ToolRegistry toolRegistry)
        {
            _toolRegistry = toolRegistry;
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to describe a specific tool by ID.
        /// </summary>
        /// <param name="request">The tool invocation request with toolId parameter.</param>
        /// <returns>Complete tool definition.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            string toolId = null;

            if (request.Arguments != null && request.Arguments.TryGetValue("toolId", out var toolIdObj))
            {
                toolId = toolIdObj as string;
            }

            if (string.IsNullOrEmpty(toolId))
            {
                // This should not happen due to validation, but handle gracefully
                toolId = "";
            }

            var definition = _toolRegistry.Describe(toolId);

            if (definition == null)
            {
                // Return empty definition structure if tool not found
                // The registry validation should catch this, but handle gracefully
                var emptyDefinition = new Dictionary<string, object>
                {
                    { "id", toolId },
                    { "error", "Tool not found" }
                };

                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "tool", emptyDefinition }
                    }
                };
            }

            // Convert ToolDefinition to dictionary for JSON serialization
            var toolDict = ConvertDefinitionToDictionary(definition);

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = new Dictionary<string, object>
                {
                    { "tool", toolDict }
                }
            };

            return response;
        }

        private Dictionary<string, object> ConvertDefinitionToDictionary(ToolDefinition definition)
        {
            var dict = new Dictionary<string, object>
            {
                { "id", definition.Id ?? "" },
                { "name", definition.Name ?? "" },
                { "description", definition.Description ?? "" },
                { "category", definition.Category ?? "" },
                { "safetyLevel", definition.SafetyLevel.ToString() },
                { "tier", definition.Tier ?? "" },
                { "schemaVersion", definition.SchemaVersion ?? "0.1.0" }
            };

            // Convert inputs (sorted for deterministic ordering)
            var inputsDict = new Dictionary<string, object>();
            if (definition.Inputs != null)
            {
                var sortedInputs = definition.Inputs.OrderBy(i => i.Key).ToList();
                foreach (var input in sortedInputs)
                {
                    inputsDict[input.Key] = ConvertParameterSchemaToDictionary(input.Value);
                }
            }
            dict["inputs"] = inputsDict;

            // Convert outputs (sorted for deterministic ordering)
            var outputsDict = new Dictionary<string, object>();
            if (definition.Outputs != null)
            {
                var sortedOutputs = definition.Outputs.OrderBy(o => o.Key).ToList();
                foreach (var output in sortedOutputs)
                {
                    outputsDict[output.Key] = ConvertOutputSchemaToDictionary(output.Value);
                }
            }
            dict["outputs"] = outputsDict;

            if (!string.IsNullOrEmpty(definition.Notes))
            {
                dict["notes"] = definition.Notes;
            }

            return dict;
        }

        private Dictionary<string, object> ConvertParameterSchemaToDictionary(ToolParameterSchema schema)
        {
            var dict = new Dictionary<string, object>
            {
                { "type", schema.Type ?? "" },
                { "required", schema.Required },
                { "description", schema.Description ?? "" }
            };

            if (schema.Default != null)
            {
                dict["default"] = schema.Default;
            }

            if (schema.Enum != null && schema.Enum.Length > 0)
            {
                dict["enum"] = schema.Enum;
            }

            if (schema.Minimum.HasValue)
            {
                dict["minimum"] = schema.Minimum.Value;
            }

            if (schema.Maximum.HasValue)
            {
                dict["maximum"] = schema.Maximum.Value;
            }

            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                var propertiesDict = new Dictionary<string, object>();
                var sortedProperties = schema.Properties.OrderBy(p => p.Key).ToList();
                foreach (var prop in sortedProperties)
                {
                    propertiesDict[prop.Key] = ConvertParameterSchemaToDictionary(prop.Value);
                }
                dict["properties"] = propertiesDict;
            }

            if (schema.Items != null)
            {
                dict["items"] = ConvertParameterSchemaToDictionary(schema.Items);
            }

            return dict;
        }

        private Dictionary<string, object> ConvertOutputSchemaToDictionary(ToolOutputSchema schema)
        {
            var dict = new Dictionary<string, object>
            {
                { "type", schema.Type ?? "" },
                { "description", schema.Description ?? "" }
            };

            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                var propertiesDict = new Dictionary<string, object>();
                var sortedProperties = schema.Properties.OrderBy(p => p.Key).ToList();
                foreach (var prop in sortedProperties)
                {
                    propertiesDict[prop.Key] = ConvertOutputSchemaToDictionary(prop.Value);
                }
                dict["properties"] = propertiesDict;
            }

            if (schema.Items != null)
            {
                dict["items"] = ConvertOutputSchemaToDictionary(schema.Items);
            }

            return dict;
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "mcp.tool.describe",
                Name = "Describe Tool",
                Description = "Returns the complete schema for a specific tool, including input parameters, output structure, and safety information. Critical for LLM grounding and human inspection.",
                Category = "mcp.platform",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>
                {
                    {
                        "toolId",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = true,
                            Description = "The ID of the tool to describe"
                        }
                    }
                },
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "tool",
                        new ToolOutputSchema
                        {
                            Type = "object",
                            Description = "Complete tool definition including schema, metadata, and safety information",
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
                                    "description",
                                    new ToolOutputSchema
                                    {
                                        Type = "string",
                                        Description = "Tool description"
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
                                        Description = "Safety level"
                                    }
                                },
                                {
                                    "tier",
                                    new ToolOutputSchema
                                    {
                                        Type = "string",
                                        Description = "Tier availability"
                                    }
                                },
                                {
                                    "schemaVersion",
                                    new ToolOutputSchema
                                    {
                                        Type = "string",
                                        Description = "Schema version"
                                    }
                                },
                                {
                                    "inputs",
                                    new ToolOutputSchema
                                    {
                                        Type = "object",
                                        Description = "Input parameter schemas"
                                    }
                                },
                                {
                                    "outputs",
                                    new ToolOutputSchema
                                    {
                                        Type = "object",
                                        Description = "Output property schemas"
                                    }
                                },
                                {
                                    "notes",
                                    new ToolOutputSchema
                                    {
                                        Type = "string",
                                        Description = "Additional notes"
                                    }
                                }
                            }
                        }
                    }
                },
                Notes = "Read-only. Returns tool schema only; does not execute the tool."
            };
        }
    }
}

