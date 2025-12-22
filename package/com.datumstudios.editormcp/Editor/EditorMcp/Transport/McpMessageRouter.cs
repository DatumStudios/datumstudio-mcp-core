using System;
using System.Collections.Generic;
using System.Linq;
using DatumStudios.EditorMCP.Registry;
using DatumStudios.EditorMCP.Schemas;
using UnityEngine;

namespace DatumStudios.EditorMCP.Transport
{
    /// <summary>
    /// Routes MCP request envelopes to ToolRegistry methods and converts responses
    /// to MCP response envelopes. Handles all MCP protocol methods: tools/call
    /// (canonical method for tool invocations), tools/list, tools/describe, and
    /// server/info (deprecated alias that routes to mcp.server.info via ToolRegistry).
    /// </summary>
    public class McpMessageRouter
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly string _serverVersion;

        /// <summary>
        /// Initializes a new instance of the McpMessageRouter class.
        /// </summary>
        /// <param name="toolRegistry">The tool registry to route requests to.</param>
        /// <param name="serverVersion">The EditorMCP server version.</param>
        public McpMessageRouter(ToolRegistry toolRegistry, string serverVersion)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _serverVersion = serverVersion ?? throw new ArgumentNullException(nameof(serverVersion));
        }

        /// <summary>
        /// Routes an MCP request to the appropriate handler and returns an MCP response.
        /// </summary>
        /// <param name="request">The MCP request envelope.</param>
        /// <returns>The MCP response envelope (success or error).</returns>
        public McpResponse Route(McpRequest request)
        {
            if (request == null)
            {
                return CreateErrorResponse(null, JsonRpcErrorCodes.InvalidRequest, "Request is null", null);
            }

            // Validate JSON-RPC version
            if (request.JsonRpc != "2.0")
            {
                return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidRequest, 
                    $"Unsupported JSON-RPC version: {request.JsonRpc}. Expected '2.0'.", null);
            }

            // Validate method
            if (string.IsNullOrEmpty(request.Method))
            {
                return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidRequest, 
                    "Method is required.", null);
            }

            try
            {
                // Route to appropriate handler based on method
                switch (request.Method)
                {
                    case "tools/call":
                        return HandleToolsCall(request);
                    case "tools/list":
                        return HandleToolsList(request);
                    case "tools/describe":
                        return HandleToolsDescribe(request);
                    case "server/info":
                        return HandleServerInfo(request);
                    default:
                        return CreateErrorResponse(request.Id, JsonRpcErrorCodes.MethodNotFound,
                            $"Method not found: {request.Method}", null);
                }
            }
            catch (Exception ex)
            {
                // Fail closed: return structured error, don't throw
                return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError,
                    $"Internal error: {ex.Message}", new Dictionary<string, object>
                    {
                        { "exception", ex.GetType().Name },
                        { "stackTrace", ex.StackTrace }
                    });
            }
        }

        private McpResponse HandleToolsCall(McpRequest request)
        {
            if (request.Params == null)
            {
                return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams,
                    "Params is required for tools/call", null);
            }

            // Extract tool ID and arguments
            if (!request.Params.TryGetValue("tool", out var toolObj) || !(toolObj is string toolId))
            {
                return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams,
                    "Params.tool (string) is required for tools/call", null);
            }

            var arguments = request.Params.TryGetValue("arguments", out var argsObj) 
                ? ConvertToDictionary(argsObj) 
                : new Dictionary<string, object>();

            // Create tool invocation request
            var invokeRequest = new ToolInvokeRequest
            {
                Tool = toolId,
                Arguments = arguments
            };

            // Invoke tool via registry through main thread dispatcher
            // This ensures all Unity API calls execute on the main thread
            ToolInvocationResult result;
            
            // Wrap the registry invocation in a dispatcher call
            // The dispatcher expects Func<ToolInvokeResponse>, so we wrap ToolRegistry.Invoke
            var dispatcherResponse = EditorMcpMainThreadDispatcher.Instance.Invoke(() =>
            {
                // This executes on the main thread - safe to call Unity APIs
                var registryResult = _toolRegistry.Invoke(toolId, invokeRequest);
                
                // Convert ToolInvocationResult to ToolInvokeResponse for dispatcher
                if (registryResult.IsSuccess)
                {
                    return registryResult.Response;
                }
                else
                {
                    // Convert error to ToolInvokeResponse format
                    return new ToolInvokeResponse
                    {
                        Tool = toolId,
                        Output = new Dictionary<string, object>
                        {
                            { "error", registryResult.Error.Message },
                            { "code", registryResult.Error.Code },
                            { "errorType", registryResult.Error.Data?.ErrorType ?? "execution" },
                            { "details", registryResult.Error.Data?.Details ?? new Dictionary<string, object>() }
                        },
                        Diagnostics = new[] { $"Tool invocation failed: {registryResult.Error.Message}" }
                    };
                }
            }, TimeSpan.FromSeconds(30));

            // Check if dispatcher returned a timeout/error response
            if (dispatcherResponse.Tool.StartsWith("internal."))
            {
                // Dispatcher returned an error (timeout or exception)
                result = ToolInvocationResult.Failure(new EditorMcpError
                {
                    Code = dispatcherResponse.Tool == "internal.timeout" 
                        ? EditorMcpErrorCodes.ToolExecutionTimeout 
                        : EditorMcpErrorCodes.ToolExecutionError,
                    Message = dispatcherResponse.Output.ContainsKey("error") 
                        ? dispatcherResponse.Output["error"].ToString() 
                        : "Tool execution failed",
                    Data = new EditorMcpErrorData
                    {
                        Tool = toolId,
                        ErrorType = dispatcherResponse.Tool == "internal.timeout" 
                            ? ErrorTypes.Timeout 
                            : ErrorTypes.Execution,
                        Details = dispatcherResponse.Output
                    }
                });
            }
            else if (dispatcherResponse.Output.ContainsKey("error"))
            {
                // Registry returned an error (converted to ToolInvokeResponse)
                result = ToolInvocationResult.Failure(new EditorMcpError
                {
                    Code = dispatcherResponse.Output.ContainsKey("code") 
                        ? Convert.ToInt32(dispatcherResponse.Output["code"]) 
                        : EditorMcpErrorCodes.ToolExecutionError,
                    Message = dispatcherResponse.Output["error"].ToString(),
                    Data = new EditorMcpErrorData
                    {
                        Tool = toolId,
                        ErrorType = dispatcherResponse.Output.ContainsKey("errorType")
                            ? dispatcherResponse.Output["errorType"].ToString()
                            : ErrorTypes.Execution,
                        Details = dispatcherResponse.Output.ContainsKey("details")
                            ? dispatcherResponse.Output["details"] as Dictionary<string, object>
                            : new Dictionary<string, object>()
                    }
                });
            }
            else
            {
                // Success - use the response directly
                result = ToolInvocationResult.Success(dispatcherResponse);
            }

            if (result.IsSuccess)
            {
                // Convert ToolInvokeResponse to MCP response
                var response = new McpResponse
                {
                    JsonRpc = "2.0",
                    Id = request.Id,
                    Result = new Dictionary<string, object>
                    {
                        { "tool", result.Response.Tool },
                        { "output", result.Response.Output }
                    }
                };

                // Add diagnostics if present
                if (result.Response.Diagnostics != null && result.Response.Diagnostics.Length > 0)
                {
                    response.Result["diagnostics"] = result.Response.Diagnostics;
                }

                return response;
            }
            else
            {
                // Convert EditorMcpError to MCP error response
                return CreateErrorResponse(request.Id, result.Error.Code, result.Error.Message,
                    new Dictionary<string, object>
                    {
                        { "tool", result.Error.Data?.Tool },
                        { "errorType", result.Error.Data?.ErrorType },
                        { "details", result.Error.Data?.Details ?? new Dictionary<string, object>() }
                    });
            }
        }

        private McpResponse HandleToolsList(McpRequest request)
        {
            string category = null;
            string tier = null;

            if (request.Params != null)
            {
                if (request.Params.TryGetValue("category", out var categoryObj) && categoryObj is string)
                {
                    category = (string)categoryObj;
                }

                if (request.Params.TryGetValue("tier", out var tierObj) && tierObj is string)
                {
                    tier = (string)tierObj;
                }
            }

            // Execute on main thread - ToolRegistry.List may use Unity APIs
            var toolSummariesResponse = EditorMcpMainThreadDispatcher.Instance.Invoke(() =>
            {
                var tools = _toolRegistry.List(category, tier);
                var summaries = tools.OrderBy(t => t.Id).Select(tool => new Dictionary<string, object>
                {
                    { "id", tool.Id },
                    { "name", tool.Name },
                    { "category", tool.Category ?? "" },
                    { "safetyLevel", tool.SafetyLevel.ToString() },
                    { "description", tool.Description ?? "" },
                    { "tier", tool.Tier ?? "" }
                }).ToArray();

                // Return as ToolInvokeResponse with data in Output
                return new ToolInvokeResponse
                {
                    Tool = "tools.list",
                    Output = new Dictionary<string, object>
                    {
                        { "tools", summaries }
                    }
                };
            }, TimeSpan.FromSeconds(10));

            // Check for dispatcher error
            if (toolSummariesResponse.Tool.StartsWith("internal."))
            {
                return CreateErrorResponse(request.Id, EditorMcpErrorCodes.ToolExecutionError,
                    toolSummariesResponse.Output.ContainsKey("error") 
                        ? toolSummariesResponse.Output["error"].ToString() 
                        : "Failed to list tools", null);
            }

            return new McpResponse
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Result = toolSummariesResponse.Output
            };
        }

        private McpResponse HandleToolsDescribe(McpRequest request)
        {
            if (request.Params == null)
            {
                return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams,
                    "Params is required for tools/describe", null);
            }

            if (!request.Params.TryGetValue("tool", out var toolObj) || !(toolObj is string toolId))
            {
                return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams,
                    "Params.tool (string) is required for tools/describe", null);
            }

            // Execute on main thread - ToolRegistry.Describe may use Unity APIs
            var toolDictResponse = EditorMcpMainThreadDispatcher.Instance.Invoke(() =>
            {
                var definition = _toolRegistry.Describe(toolId);
                if (definition == null)
                {
                    // Return null in Output to indicate not found
                    return new ToolInvokeResponse
                    {
                        Tool = "tools.describe",
                        Output = new Dictionary<string, object>
                        {
                            { "tool", null },
                            { "notFound", true }
                        }
                    };
                }

                // Convert ToolDefinition to dictionary (simplified - full conversion would be more complex)
                var toolDict = ConvertToolDefinitionToDictionary(definition);

                return new ToolInvokeResponse
                {
                    Tool = "tools.describe",
                    Output = new Dictionary<string, object>
                    {
                        { "tool", toolDict }
                    }
                };
            }, TimeSpan.FromSeconds(10));

            // Check for dispatcher error
            if (toolDictResponse.Tool.StartsWith("internal."))
            {
                return CreateErrorResponse(request.Id, EditorMcpErrorCodes.ToolExecutionError,
                    toolDictResponse.Output.ContainsKey("error") 
                        ? toolDictResponse.Output["error"].ToString() 
                        : "Failed to describe tool", null);
            }

            // Check if tool was not found
            if (toolDictResponse.Output.ContainsKey("notFound") && 
                toolDictResponse.Output["notFound"] is bool notFound && 
                notFound)
            {
                return CreateErrorResponse(request.Id, EditorMcpErrorCodes.ToolNotFound,
                    $"Tool not found: {toolId}", new Dictionary<string, object>
                    {
                        { "tool", toolId },
                        { "errorType", ErrorTypes.NotFound }
                    });
            }

            return new McpResponse
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Result = toolDictResponse.Output
            };
        }

        /// <summary>
        /// Handles the legacy "server/info" method by routing to mcp.server.info via ToolRegistry.
        /// This is a deprecated alias that maintains backward compatibility while ensuring
        /// all tool invocations go through the canonical ToolRegistry path.
        /// </summary>
        private McpResponse HandleServerInfo(McpRequest request)
        {
            // Route to mcp.server.info tool via ToolRegistry (same path as tools/call)
            const string toolId = "mcp.server.info";
            var invokeRequest = new ToolInvokeRequest
            {
                Tool = toolId,
                Arguments = new Dictionary<string, object>() // mcp.server.info takes no arguments
            };

            // Invoke tool via registry through main thread dispatcher
            // This ensures all Unity API calls execute on the main thread
            ToolInvocationResult result;
            
            // Wrap the registry invocation in a dispatcher call
            var dispatcherResponse = EditorMcpMainThreadDispatcher.Instance.Invoke(() =>
            {
                // This executes on the main thread - safe to call Unity APIs
                var registryResult = _toolRegistry.Invoke(toolId, invokeRequest);
                
                // Convert ToolInvocationResult to ToolInvokeResponse for dispatcher
                if (registryResult.IsSuccess)
                {
                    // Add deprecation diagnostic to the response
                    var response = registryResult.Response;
                    var diagnostics = new List<string>(response.Diagnostics ?? new string[0]);
                    diagnostics.Add("The 'server/info' method is deprecated. Use 'tools/call' with tool 'mcp.server.info' instead.");
                    
                    return new ToolInvokeResponse
                    {
                        Tool = response.Tool,
                        Output = response.Output,
                        Diagnostics = diagnostics.ToArray()
                    };
                }
                else
                {
                    // Convert error to ToolInvokeResponse format
                    return new ToolInvokeResponse
                    {
                        Tool = toolId,
                        Output = new Dictionary<string, object>
                        {
                            { "error", registryResult.Error.Message },
                            { "code", registryResult.Error.Code },
                            { "errorType", registryResult.Error.Data?.ErrorType ?? "execution" },
                            { "details", registryResult.Error.Data?.Details ?? new Dictionary<string, object>() }
                        },
                        Diagnostics = new[] { $"Tool invocation failed: {registryResult.Error.Message}" }
                    };
                }
            }, TimeSpan.FromSeconds(30));

            // Check if dispatcher returned a timeout/error response
            if (dispatcherResponse.Tool.StartsWith("internal."))
            {
                // Dispatcher returned an error (timeout or exception)
                result = ToolInvocationResult.Failure(new EditorMcpError
                {
                    Code = dispatcherResponse.Tool == "internal.timeout" 
                        ? EditorMcpErrorCodes.ToolExecutionTimeout 
                        : EditorMcpErrorCodes.ToolExecutionError,
                    Message = dispatcherResponse.Output.ContainsKey("error") 
                        ? dispatcherResponse.Output["error"].ToString() 
                        : "Tool execution failed",
                    Data = new EditorMcpErrorData
                    {
                        Tool = toolId,
                        ErrorType = dispatcherResponse.Tool == "internal.timeout" 
                            ? ErrorTypes.Timeout 
                            : ErrorTypes.Execution,
                        Details = dispatcherResponse.Output
                    }
                });
            }
            else if (dispatcherResponse.Output.ContainsKey("error"))
            {
                // Registry returned an error (converted to ToolInvokeResponse)
                result = ToolInvocationResult.Failure(new EditorMcpError
                {
                    Code = dispatcherResponse.Output.ContainsKey("code") 
                        ? Convert.ToInt32(dispatcherResponse.Output["code"]) 
                        : EditorMcpErrorCodes.ToolExecutionError,
                    Message = dispatcherResponse.Output["error"].ToString(),
                    Data = new EditorMcpErrorData
                    {
                        Tool = toolId,
                        ErrorType = dispatcherResponse.Output.ContainsKey("errorType")
                            ? dispatcherResponse.Output["errorType"].ToString()
                            : ErrorTypes.Execution,
                        Details = dispatcherResponse.Output.ContainsKey("details")
                            ? dispatcherResponse.Output["details"] as Dictionary<string, object>
                            : new Dictionary<string, object>()
                    }
                });
            }
            else
            {
                // Success - use the response directly
                result = ToolInvocationResult.Success(dispatcherResponse);
            }

            if (result.IsSuccess)
            {
                // Convert ToolInvokeResponse to MCP response (canonical tools/call shape)
                var response = new McpResponse
                {
                    JsonRpc = "2.0",
                    Id = request.Id,
                    Result = new Dictionary<string, object>
                    {
                        { "tool", result.Response.Tool },
                        { "output", result.Response.Output }
                    }
                };

                // Add diagnostics if present
                if (result.Response.Diagnostics != null && result.Response.Diagnostics.Length > 0)
                {
                    response.Result["diagnostics"] = result.Response.Diagnostics;
                }

                return response;
            }
            else
            {
                // Convert EditorMcpError to MCP error response
                return CreateErrorResponse(request.Id, result.Error.Code, result.Error.Message,
                    new Dictionary<string, object>
                    {
                        { "tool", result.Error.Data?.Tool },
                        { "errorType", result.Error.Data?.ErrorType },
                        { "details", result.Error.Data?.Details ?? new Dictionary<string, object>() }
                    });
            }
        }

        private McpResponse CreateErrorResponse(object id, int code, string message, Dictionary<string, object> data)
        {
            return new McpResponse
            {
                JsonRpc = "2.0",
                Id = id,
                Error = new McpError
                {
                    Code = code,
                    Message = message,
                    Data = data ?? new Dictionary<string, object>()
                }
            };
        }

        private Dictionary<string, object> ConvertToDictionary(object obj)
        {
            if (obj == null)
                return new Dictionary<string, object>();

            if (obj is Dictionary<string, object> dict)
                return dict;

            // For other types, try to serialize and deserialize
            // This is a simplified approach - in production, you might want more robust conversion
            try
            {
                string json = JsonUtility.ToJson(obj);
                // JsonUtility doesn't support Dictionary<string, object> directly
                // For v0.1, we'll use a simpler approach
                return new Dictionary<string, object> { { "value", obj } };
            }
            catch
            {
                return new Dictionary<string, object> { { "value", obj } };
            }
        }

        private Dictionary<string, object> ConvertToolDefinitionToDictionary(ToolDefinition definition)
        {
            // Simplified conversion - full implementation would convert all nested schemas
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

            // Note: Full schema conversion would require more complex serialization
            // For v0.1, we'll include basic fields. Full schema conversion can be added later.
            if (!string.IsNullOrEmpty(definition.Notes))
            {
                dict["notes"] = definition.Notes;
            }

            return dict;
        }
    }
}

