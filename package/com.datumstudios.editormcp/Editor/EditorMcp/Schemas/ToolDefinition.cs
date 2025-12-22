using System;
using System.Collections.Generic;

namespace DatumStudios.EditorMCP.Schemas
{
    /// <summary>
    /// Defines a tool's identity, behavior, inputs, outputs, and metadata.
    /// </summary>
    public class ToolDefinition
    {
        /// <summary>
        /// The canonical tool identifier. Must be unique and follow the naming convention:
        /// category.subcategory.action (e.g., "scene.hierarchy.dump").
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Human-readable display name for the tool.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Detailed description of what the tool does, its purpose, and when to use it.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Tool category for grouping and filtering (e.g., "scene", "project", "asset").
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Safety level indicating what operations the tool can perform.
        /// </summary>
        public SafetyLevel SafetyLevel { get; set; }

        /// <summary>
        /// Tier that provides access to this tool. Core v0.1 tools are "core".
        /// </summary>
        public string Tier { get; set; }

        /// <summary>
        /// Schema describing the tool's input parameters.
        /// Key is parameter name, value describes the parameter (type, required, description).
        /// </summary>
        public Dictionary<string, ToolParameterSchema> Inputs { get; set; }

        /// <summary>
        /// Schema describing the tool's output structure.
        /// Key is property name, value describes the property (type, description).
        /// </summary>
        public Dictionary<string, ToolOutputSchema> Outputs { get; set; }

        /// <summary>
        /// Additional notes about the tool (limitations, performance considerations, warnings).
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// Schema version for inputs/outputs. Used to version the schema format itself.
        /// </summary>
        public string SchemaVersion { get; set; }

        /// <summary>
        /// Initializes a new instance of the ToolDefinition class.
        /// </summary>
        public ToolDefinition()
        {
            Inputs = new Dictionary<string, ToolParameterSchema>();
            Outputs = new Dictionary<string, ToolOutputSchema>();
            SchemaVersion = "0.1.0";
        }
    }

    /// <summary>
    /// Describes a tool input parameter. Supports nested properties for object types.
    /// </summary>
    public class ToolParameterSchema
    {
        /// <summary>
        /// Parameter type (e.g., "string", "integer", "number", "boolean", "array", "object").
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Whether this parameter is required.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Description of the parameter.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Default value for optional parameters.
        /// </summary>
        public object Default { get; set; }

        /// <summary>
        /// Valid enum values (if parameter is constrained to specific values).
        /// </summary>
        public string[] Enum { get; set; }

        /// <summary>
        /// Minimum value (for numeric types).
        /// </summary>
        public double? Minimum { get; set; }

        /// <summary>
        /// Maximum value (for numeric types).
        /// </summary>
        public double? Maximum { get; set; }

        /// <summary>
        /// For object types, describes the nested properties.
        /// Key is property name, value is the property schema.
        /// </summary>
        public Dictionary<string, ToolParameterSchema> Properties { get; set; }

        /// <summary>
        /// For array types, describes the item schema.
        /// </summary>
        public ToolParameterSchema Items { get; set; }

        /// <summary>
        /// Initializes a new instance of the ToolParameterSchema class.
        /// </summary>
        public ToolParameterSchema()
        {
            Properties = new Dictionary<string, ToolParameterSchema>();
        }
    }

    /// <summary>
    /// Describes a tool output property. Supports nested properties for object types.
    /// </summary>
    public class ToolOutputSchema
    {
        /// <summary>
        /// Property type (e.g., "string", "integer", "number", "boolean", "array", "object").
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Description of the property.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// For array types, describes the item structure.
        /// </summary>
        public ToolOutputSchema Items { get; set; }

        /// <summary>
        /// For object types, describes the nested properties.
        /// Key is property name, value is the property schema.
        /// </summary>
        public Dictionary<string, ToolOutputSchema> Properties { get; set; }

        /// <summary>
        /// Initializes a new instance of the ToolOutputSchema class.
        /// </summary>
        public ToolOutputSchema()
        {
            Properties = new Dictionary<string, ToolOutputSchema>();
        }
    }
}

