using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using DatumStudios.EditorMCP.Schemas;
using DatumStudios.EditorMCP.Tools;

namespace DatumStudios.EditorMCP.Registry
{
    /// <summary>
    /// Information about an attribute-based tool (static method with [McpTool] attribute).
    /// </summary>
    internal class AttributeToolInfo
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public Tier MinTier { get; set; }
        public MethodInfo Method { get; set; }
        public string Category { get; set; }
    }

    /// <summary>
    /// Central registry for tool definitions and implementations. Manages tool registration,
    /// discovery, metadata, and invocation with validation.
    /// Supports both legacy interface-based tools and new attribute-based tools.
    /// </summary>
    public class ToolRegistry
    {
        private readonly Dictionary<string, ToolDefinition> _definitions = new Dictionary<string, ToolDefinition>();
        private readonly Dictionary<string, IEditorMcpTool> _implementations = new Dictionary<string, IEditorMcpTool>();
        private readonly Dictionary<string, AttributeToolInfo> _attributeTools = new Dictionary<string, AttributeToolInfo>();
        private static bool _attributeDiscoveryPerformed = false;
        private static ToolRegistry _current;

        /// <summary>
        /// Gets or sets the current ToolRegistry instance (for static tool access).
        /// Set by EditorMcpServer when it starts.
        /// </summary>
        public static ToolRegistry Current
        {
            get => _current;
            set => _current = value;
        }

        /// <summary>
        /// Gets the number of registered tools (both interface-based and attribute-based).
        /// </summary>
        public int Count => _definitions.Count + _attributeTools.Count;

        /// <summary>
        /// Gets the number of attribute-based tools discovered.
        /// </summary>
        public int AttributeToolCount => _attributeTools.Count;

        /// <summary>
        /// Performs automatic discovery of tools marked with [McpTool] attributes.
        /// Called automatically on Editor startup via [InitializeOnLoadMethod].
        /// </summary>
        [InitializeOnLoadMethod]
        private static void InitializeAttributeDiscovery()
        {
            if (_attributeDiscoveryPerformed)
            {
                return;
            }

            // Create a temporary instance to perform discovery
            // The actual instance will be created by EditorMcpServer later
            var tempInstance = new ToolRegistry();
            tempInstance.DiscoverAttributeTools();
            _attributeDiscoveryPerformed = true;
        }

        /// <summary>
        /// Discovers and registers all tools marked with [McpTool] attributes in the current assembly.
        /// </summary>
        public void DiscoverAttributeTools()
        {
            _attributeTools.Clear();

            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                // Check for category attribute on class
                var categoryAttr = type.GetCustomAttribute<McpToolCategoryAttribute>();
                var category = categoryAttr?.Category ?? ExtractCategoryFromNamespace(type);

                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<McpToolAttribute>();
                    if (attr != null)
                    {
                        // Check tier access
                        if (!LicenseManager.HasTier(attr.MinTier))
                        {
                            continue; // Skip tools that require a higher tier
                        }

                        // Validate method signature: must be static and accept string parameter
                        var parameters = method.GetParameters();
                        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string))
                        {
                            UnityEngine.Debug.LogWarning($"[EditorMCP] Tool '{attr.Id}' has invalid signature. Expected: static string Method(string json)");
                            continue;
                        }

                        if (method.ReturnType != typeof(string))
                        {
                            UnityEngine.Debug.LogWarning($"[EditorMCP] Tool '{attr.Id}' has invalid return type. Expected: string");
                            continue;
                        }

                        // Register the attribute-based tool
                        if (_attributeTools.ContainsKey(attr.Id))
                        {
                            UnityEngine.Debug.LogWarning($"[EditorMCP] Duplicate tool ID '{attr.Id}' found. Skipping duplicate.");
                            continue;
                        }

                        _attributeTools[attr.Id] = new AttributeToolInfo
                        {
                            Id = attr.Id,
                            Description = attr.Description,
                            MinTier = attr.MinTier,
                            Method = method,
                            Category = category
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Extracts category from namespace or type name as fallback.
        /// </summary>
        private string ExtractCategoryFromNamespace(Type type)
        {
            var ns = type.Namespace ?? string.Empty;
            if (ns.Contains("Tools"))
            {
                // Try to extract category from namespace like "DatumStudios.EditorMCP.Tools"
                var parts = ns.Split('.');
                if (parts.Length > 0)
                {
                    return parts[parts.Length - 1].ToLower();
                }
            }

            // Fallback: use type name
            var typeName = type.Name;
            if (typeName.EndsWith("Tools"))
            {
                return typeName.Substring(0, typeName.Length - 5).ToLower();
            }

            return "mcp"; // Default category
        }

        /// <summary>
        /// Registers a tool definition. The definition must match the tool's Definition property.
        /// </summary>
        /// <param name="tool">The tool implementation to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when tool is null.</exception>
        /// <exception cref="ArgumentException">Thrown when tool definition is invalid or already registered.</exception>
        public void Register(IEditorMcpTool tool)
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));

            if (tool.Definition == null)
                throw new ArgumentException("Tool must have a non-null Definition.", nameof(tool));

            var definition = tool.Definition;

            if (string.IsNullOrEmpty(definition.Id))
                throw new ArgumentException("Tool definition must have a non-empty Id.", nameof(tool));

            if (_definitions.ContainsKey(definition.Id))
                throw new ArgumentException($"Tool with ID '{definition.Id}' is already registered.", nameof(tool));

            // Validate that tool ID matches definition ID
            if (!string.Equals(tool.Definition.Id, definition.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Tool implementation ID mismatch. Expected '{definition.Id}'.", nameof(tool));
            }

            _definitions[definition.Id] = definition;
            _implementations[definition.Id] = tool;
        }

        /// <summary>
        /// Registers a tool definition without an implementation (for discovery/description only).
        /// </summary>
        /// <param name="definition">The tool definition to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when definition is null.</exception>
        /// <exception cref="ArgumentException">Thrown when tool ID is null, empty, or already registered.</exception>
        public void Register(ToolDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            if (string.IsNullOrEmpty(definition.Id))
                throw new ArgumentException("Tool definition must have a non-empty Id.", nameof(definition));

            if (_definitions.ContainsKey(definition.Id))
                throw new ArgumentException($"Tool with ID '{definition.Id}' is already registered.", nameof(definition));

            _definitions[definition.Id] = definition;
        }

        /// <summary>
        /// Gets a tool definition by ID. Checks both interface-based and attribute-based tools.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <returns>The tool definition, or null if not found.</returns>
        public ToolDefinition Get(string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
                return null;

            // Check interface-based tools first
            if (_definitions.TryGetValue(toolId, out var definition))
            {
                return definition;
            }

            // Check attribute-based tools
            if (_attributeTools.TryGetValue(toolId, out var attrTool))
            {
                // Create a ToolDefinition from attribute tool info
                return new ToolDefinition
                {
                    Id = attrTool.Id,
                    Description = attrTool.Description,
                    Category = attrTool.Category,
                    Tier = attrTool.MinTier.ToString().ToLower(),
                    Name = attrTool.Id
                };
            }

            return null;
        }

        /// <summary>
        /// Lists all registered tools, optionally filtered by category and tier.
        /// Includes both interface-based and attribute-based tools.
        /// </summary>
        /// <param name="category">Optional category filter.</param>
        /// <param name="tier">Optional tier filter.</param>
        /// <returns>List of tool definitions matching the filters.</returns>
        public List<ToolDefinition> List(string category = null, string tier = null)
        {
            var allTools = new List<ToolDefinition>();

            // Add interface-based tools
            allTools.AddRange(_definitions.Values);

            // Add attribute-based tools (convert to ToolDefinition)
            foreach (var attrTool in _attributeTools.Values)
            {
                allTools.Add(new ToolDefinition
                {
                    Id = attrTool.Id,
                    Description = attrTool.Description,
                    Category = attrTool.Category,
                    Tier = attrTool.MinTier.ToString().ToLower(),
                    Name = attrTool.Id
                });
            }

            var query = allTools.AsEnumerable();

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(tier))
            {
                query = query.Where(t => string.Equals(t.Tier, tier, StringComparison.OrdinalIgnoreCase));
            }

            return query.ToList();
        }

        /// <summary>
        /// Describes a tool by returning its complete definition.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <returns>The tool definition, or null if not found.</returns>
        public ToolDefinition Describe(string toolId)
        {
            return Get(toolId);
        }

        /// <summary>
        /// Invokes a tool by ID with the given request. Performs validation before invocation.
        /// Supports both interface-based and attribute-based tools.
        /// </summary>
        /// <param name="toolId">The tool ID to invoke.</param>
        /// <param name="request">The tool invocation request.</param>
        /// <returns>Result containing either the response or an error.</returns>
        public ToolInvocationResult Invoke(string toolId, ToolInvokeRequest request)
        {
            if (string.IsNullOrEmpty(toolId))
            {
                return ToolInvocationResult.Failure(CreateToolNotFoundError(toolId));
            }

            // Check for attribute-based tool first
            if (_attributeTools.TryGetValue(toolId, out var attrTool))
            {
                return InvokeAttributeTool(toolId, attrTool, request);
            }

            // Check for interface-based tool
            if (!_definitions.TryGetValue(toolId, out var definition))
            {
                return ToolInvocationResult.Failure(CreateToolNotFoundError(toolId));
            }

            // Check if tool has an implementation
            if (!_implementations.TryGetValue(toolId, out var tool))
            {
                return ToolInvocationResult.Failure(new EditorMcpError
                {
                    Code = EditorMcpErrorCodes.ToolExecutionError,
                    Message = $"Tool '{toolId}' is registered but has no implementation.",
                    Data = new EditorMcpErrorData
                    {
                        Tool = toolId,
                        ErrorType = ErrorTypes.Execution,
                        Details = new Dictionary<string, object>
                        {
                            { "reason", "no_implementation" }
                        }
                    }
                });
            }

            // Validate input arguments
            if (request == null)
            {
                request = new ToolInvokeRequest { Tool = toolId };
            }

            var validationResult = ToolInputValidator.Validate(definition, request.Arguments);
            if (!validationResult.IsValid)
            {
                return ToolInvocationResult.Failure(CreateValidationError(toolId, validationResult));
            }

            // Invoke the tool
            try
            {
                var response = tool.Invoke(request);
                return ToolInvocationResult.Success(response);
            }
            catch (Exception ex)
            {
                return ToolInvocationResult.Failure(new EditorMcpError
                {
                    Code = EditorMcpErrorCodes.ToolExecutionError,
                    Message = $"Tool execution failed: {ex.Message}",
                    Data = new EditorMcpErrorData
                    {
                        Tool = toolId,
                        ErrorType = ErrorTypes.Execution,
                        Details = new Dictionary<string, object>
                        {
                            { "exception", ex.GetType().Name },
                            { "message", ex.Message }
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Invokes an attribute-based tool (static method with [McpTool] attribute).
        /// </summary>
        private ToolInvocationResult InvokeAttributeTool(string toolId, AttributeToolInfo attrTool, ToolInvokeRequest request)
        {
            try
            {
                // Serialize arguments to JSON string
                string jsonParams = "{}";
                if (request?.Arguments != null && request.Arguments.Count > 0)
                {
                    jsonParams = UnityEngine.JsonUtility.ToJson(request.Arguments);
                }

                // Invoke the static method
                var result = attrTool.Method.Invoke(null, new object[] { jsonParams });

                if (result is string jsonResult)
                {
                    // Parse the JSON result and create a ToolInvokeResponse
                    // Note: The attribute-based tools return JSON strings directly
                    var response = new ToolInvokeResponse
                    {
                        Tool = toolId,
                        Output = new Dictionary<string, object>
                        {
                            { "result", jsonResult }
                        }
                    };

                    return ToolInvocationResult.Success(response);
                }

                return ToolInvocationResult.Failure(new EditorMcpError
                {
                    Code = EditorMcpErrorCodes.ToolExecutionError,
                    Message = $"Tool '{toolId}' returned invalid result type.",
                    Data = new EditorMcpErrorData
                    {
                        Tool = toolId,
                        ErrorType = ErrorTypes.Execution
                    }
                });
            }
            catch (Exception ex)
            {
                return ToolInvocationResult.Failure(new EditorMcpError
                {
                    Code = EditorMcpErrorCodes.ToolExecutionError,
                    Message = $"Tool execution failed: {ex.Message}",
                    Data = new EditorMcpErrorData
                    {
                        Tool = toolId,
                        ErrorType = ErrorTypes.Execution,
                        Details = new Dictionary<string, object>
                        {
                            { "exception", ex.GetType().Name },
                            { "message", ex.Message },
                            { "stackTrace", ex.StackTrace }
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Checks if a tool is registered (checks both interface-based and attribute-based tools).
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <returns>True if the tool is registered, false otherwise.</returns>
        public bool IsRegistered(string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
            {
                return false;
            }

            return _definitions.ContainsKey(toolId) || _attributeTools.ContainsKey(toolId);
        }

        /// <summary>
        /// Clears all registered tools and implementations (both interface-based and attribute-based).
        /// </summary>
        public void Clear()
        {
            _definitions.Clear();
            _implementations.Clear();
            _attributeTools.Clear();
        }

        /// <summary>
        /// Gets a JSON schema representation of all discovered tools.
        /// Used by mcp.tools.list tool.
        /// </summary>
        /// <returns>JSON string containing tool schema information.</returns>
        public string GetSchema()
        {
            var tools = new List<object>();

            // Add interface-based tools
            foreach (var def in _definitions.Values)
            {
                tools.Add(new
                {
                    id = def.Id,
                    name = def.Name,
                    description = def.Description,
                    category = def.Category,
                    tier = def.Tier
                });
            }

            // Add attribute-based tools
            foreach (var attrTool in _attributeTools.Values)
            {
                tools.Add(new
                {
                    id = attrTool.Id,
                    name = attrTool.Id,
                    description = attrTool.Description,
                    category = attrTool.Category,
                    tier = attrTool.MinTier.ToString().ToLower()
                });
            }

            return UnityEngine.JsonUtility.ToJson(new { tools = tools.ToArray() }, true);
        }

        private EditorMcpError CreateToolNotFoundError(string toolId)
        {
            return new EditorMcpError
            {
                Code = EditorMcpErrorCodes.ToolNotFound,
                Message = $"Tool not found: {toolId ?? "(null)"}",
                Data = new EditorMcpErrorData
                {
                    Tool = toolId,
                    ErrorType = ErrorTypes.NotFound,
                    Details = new Dictionary<string, object>()
                }
            };
        }

        private EditorMcpError CreateValidationError(string toolId, ValidationResult validationResult)
        {
            var errorMessages = validationResult.Errors.Select(e => $"{e.FieldPath}: {e.Message}").ToArray();
            var errorMessage = $"Invalid tool arguments for '{toolId}': " + string.Join("; ", errorMessages);

            var details = new Dictionary<string, object>
            {
                { "errors", validationResult.Errors.Select(e => new Dictionary<string, object>
                    {
                        { "fieldPath", e.FieldPath },
                        { "message", e.Message }
                    }).ToArray()
                }
            };

            return new EditorMcpError
            {
                Code = EditorMcpErrorCodes.InvalidToolArguments,
                Message = errorMessage,
                Data = new EditorMcpErrorData
                {
                    Tool = toolId,
                    ErrorType = ErrorTypes.Validation,
                    Details = details
                }
            };
        }
    }
}

