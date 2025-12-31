using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using DatumStudios.EditorMCP.Schemas;
using DatumStudios.EditorMCP.Diagnostics;

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
    /// Uses attribute-based tool discovery via [McpTool] attributes on static methods.
    /// </summary>
    public class ToolRegistry
    {
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
        /// Gets the number of registered tools.
        /// </summary>
        public int Count => _attributeTools.Count;

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

            var tempInstance = new ToolRegistry();
            tempInstance.DiscoverAttributeTools();
            _attributeDiscoveryPerformed = true;
        }

        /// <summary>
        /// Discovers and registers all tools marked with [McpTool] attributes in current assembly.
        /// </summary>
        public void DiscoverAttributeTools(bool forceRediscovery = false)
        {
            // Only clear and rediscover if forced or first discovery
            if (!forceRediscovery && _attributeTools.Count > 0)
            {
                return;
            }

            _attributeTools.Clear();

            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes();

            int discoveredCount = 0;
            int skippedCount = 0;
            var skippedTools = new List<string>();
            var discoveredTools = new List<string>();
            var expectedCoreTools = 19;

            foreach (var type in types)
            {
                var categoryAttr = type.GetCustomAttribute<McpToolCategoryAttribute>();
                var category = categoryAttr?.Category ?? ExtractCategoryFromNamespace(type);

                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<McpToolAttribute>();
                    if (attr != null)
                    {
                        if (!LicenseManager.HasTier(attr.MinTier))
                        {
                            skippedCount++;
                            skippedTools.Add($"{type.Name}.{method.Name} (insufficient tier: {attr.MinTier})");
                            continue;
                        }

                        if (_attributeTools.ContainsKey(attr.Id))
                        {
                            Debug.LogWarning($"[EditorMCP] Duplicate tool ID '{attr.Id}' found. Skipping duplicate.");
                            skippedCount++;
                            skippedTools.Add($"{type.Name}.{method.Name} (duplicate ID)");
                            continue;
                        }

                        var parameters = method.GetParameters();
                        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string))
                        {
                            Debug.LogWarning($"[EditorMCP] Tool '{attr.Id}' has invalid signature. Expected: static object Method(string json)");
                            continue;
                        }

                        // Accept object or Dictionary<string, object> return types (for JSON serialization)
                        if (method.ReturnType != typeof(object) && method.ReturnType != typeof(Dictionary<string, object>))
                        {
                            Debug.LogWarning($"[EditorMCP] Tool '{attr.Id}' has invalid return type. Expected: object or Dictionary<string, object>");
                            continue;
                        }

                        var toolInfo = new AttributeToolInfo
                        {
                            Id = attr.Id,
                            Description = attr.Description,
                            MinTier = attr.MinTier,
                            Method = method,
                            Category = category
                        };

                        _attributeTools[attr.Id] = toolInfo;
                        discoveredCount++;
                        discoveredTools.Add($"{type.Name}.{method.Name} (ID: {attr.Id})");
                }
            }
        }

            if (discoveredCount > 0)
            {
                UnityEngine.Debug.Log($"[EditorMCP] Discovered {discoveredCount} tools: {string.Join(", ", discoveredTools.Take(10))}");
            }
            if (skippedCount > 0)
            {
                UnityEngine.Debug.Log($"[EditorMCP] Skipped {skippedCount} tools: {string.Join(", ", skippedTools.Take(5))}");
            }

            if (discoveredCount == expectedCoreTools)
            {
                UnityEngine.Debug.Log($"[EditorMCP] ✓ Tool count validation passed: {discoveredCount} tools (expected {expectedCoreTools} Core tools)");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[EditorMCP] ⚠ Tool count validation: {discoveredCount} tools discovered (expected {expectedCoreTools} Core tools)");
            }

            UnityEngine.Debug.Log($"[EditorMCP] ====================================");
        }

        /// <summary>
        /// Extracts category from namespace or type name as fallback.
        /// </summary>
        private static string ExtractCategoryFromNamespace(Type type)
        {
            var ns = type.Namespace ?? string.Empty;
            if (ns.Contains("Tools"))
            {
                var parts = ns.Split('.');
                if (parts.Length > 0)
                {
                    return parts[parts.Length - 1].ToLower();
                }
            }

            var typeName = type.Name;
            if (typeName.EndsWith("Tools"))
            {
                return typeName.Substring(0, typeName.Length - 5).ToLower();
            }

            return "mcp";
        }

        /// <summary>
        /// Gets the tool definition for the specified tool ID.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <returns>The tool definition, or null if not found.</returns>
        public ToolDefinition Describe(string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
            {
                return null;
            }

            if (_attributeTools.TryGetValue(toolId, out var attrInfo))
            {
                return new ToolDefinition
                {
                    Id = attrInfo.Id,
                    Name = attrInfo.Id,
                    Description = attrInfo.Description ?? "",
                    Category = attrInfo.Category,
                    SafetyLevel = SafetyLevel.ReadOnly,
                    Tier = attrInfo.MinTier.ToString().ToLower()
                };
            }

            return null;
        }

        /// <summary>
        /// Lists all registered tools.
        /// </summary>
        /// <returns>List of tool definitions.</returns>
        public List<ToolDefinition> List()
        {
            var tools = new List<ToolDefinition>();
            foreach (var kvp in _attributeTools)
            {
                var attrInfo = kvp.Value;
                tools.Add(new ToolDefinition
                {
                    Id = attrInfo.Id,
                    Name = attrInfo.Id,
                    Description = attrInfo.Description ?? "",
                    Category = attrInfo.Category,
                    SafetyLevel = SafetyLevel.ReadOnly,
                    Tier = attrInfo.MinTier.ToString().ToLower()
                });
            }
            return tools;
        }

        /// <summary>
        /// Lists tools filtered by category and/or tier.
        /// </summary>
        /// <param name="category">Optional category filter.</param>
        /// <param name="tier">Optional tier filter.</param>
        /// <returns>List of filtered tool definitions.</returns>
        public List<ToolDefinition> List(string category, string tier)
        {
            var tools = new List<ToolDefinition>();
            foreach (var kvp in _attributeTools)
            {
                var attrInfo = kvp.Value;
                tools.Add(new ToolDefinition
                {
                    Id = attrInfo.Id,
                    Name = attrInfo.Id,
                    Description = attrInfo.Description ?? "",
                    Category = attrInfo.Category,
                    SafetyLevel = SafetyLevel.ReadOnly,
                    Tier = attrInfo.MinTier.ToString().ToLower()
                });
            }

            if (!string.IsNullOrEmpty(category))
            {
                tools = tools.Where(t => t.Category == category).ToList();
            }

            if (!string.IsNullOrEmpty(tier))
            {
                tools = tools.Where(t => t.Tier == tier).ToList();
            }

            return tools;
        }

        /// <summary>
        /// Checks if a tool is registered.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <returns>True if the tool is registered, false otherwise.</returns>
        public bool IsRegistered(string toolId)
        {
            return !string.IsNullOrEmpty(toolId) && _attributeTools.ContainsKey(toolId);
        }

        /// <summary>
        /// Creates a tool not found error.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <returns>Tool not found error.</returns>
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

        /// <summary>
        /// Creates a tool validation error.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <param name="validationResult">The validation result with errors.</param>
        /// <returns>Tool validation error.</returns>
        private EditorMcpError CreateValidationError(string toolId, ValidationResult validationResult)
        {
            var errorMessages = validationResult.Errors.Select(e => $"{e.FieldPath}: {e.Message}").ToArray();
            var errorMessage = $"Invalid tool arguments for '{toolId}': " + string.Join("; ", errorMessages);

            return new EditorMcpError
            {
                Code = EditorMcpErrorCodes.InvalidToolArguments,
                Message = errorMessage,
                Data = new EditorMcpErrorData
                {
                    Tool = toolId,
                    ErrorType = ErrorTypes.Validation,
                    Details = new Dictionary<string, object>
                    {
                        { "validationErrors", validationResult.Errors }
                    }
                }
            };
        }

        /// <summary>
        /// Invoke a tool by ID and return response.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <param name="jsonParams">JSON parameters string.</param>
        /// <returns>The tool execution result.</returns>
        internal ToolInvocationResult Invoke(string toolId, string jsonParams)
        {
            try
            {
                var args = string.IsNullOrEmpty(jsonParams) ? new Dictionary<string, object>() : UnityEngine.JsonUtility.FromJson<Dictionary<string, object>>(jsonParams);

                var toolResult = InvokeToolViaReflection(toolId, args);
                Dictionary<string, object> output = null;

                if (toolResult is Dictionary<string, object> dict)
                {
                    output = dict;
                }
                else if (toolResult is System.Collections.IEnumerable && !(toolResult is string))
                {
                    output = new Dictionary<string, object> { { "items", toolResult } };
                }
                else if (toolResult == null)
                {
                    output = new Dictionary<string, object>();
                }
                else
                {
                    output = new Dictionary<string, object> { { "value", toolResult.ToString() } };
                }

                var response = new ToolInvokeResponse
                {
                    Tool = toolId,
                    Output = output
                };
                return ToolInvocationResult.Success(response);
            }
            catch (Exception ex)
            {
                var error = CreateValidationError(toolId, new ValidationResult { Errors = new List<ValidationError> { new ValidationError { FieldPath = "Invocation", Message = ex.Message } } });
                return ToolInvocationResult.Failure(error);
            }
        }

        /// <summary>
        /// Invokes a tool method via reflection with exception handling.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <param name="args">Tool arguments.</param>
        /// <returns>Tool execution result object.</returns>
        private object InvokeToolViaReflection(string toolId, Dictionary<string, object> args)
        {
            if (!_attributeTools.TryGetValue(toolId, out var toolInfo))
            {
                return new Dictionary<string, object>
                {
                    { "error", $"Tool not found: {toolId}" }
                };
            }

            try
            {
                var jsonParams = args.Count > 0 ? UnityEngine.JsonUtility.ToJson(args) : "{}";
                var result = toolInfo.Method.Invoke(null, new object[] { jsonParams });
                
                // Tools now return Dictionary<string, object> directly, no need to parse JSON
                return result;
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "error", $"Tool '{toolId}' execution failed: {ex.Message}" }
                };
            }
        }
    }
}