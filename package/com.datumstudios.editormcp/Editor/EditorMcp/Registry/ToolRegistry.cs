using System;
using System.Collections.Generic;
using System.Linq;
using DatumStudios.EditorMCP.Schemas;
using DatumStudios.EditorMCP.Tools;

namespace DatumStudios.EditorMCP.Registry
{
    /// <summary>
    /// Central registry for tool definitions and implementations. Manages tool registration,
    /// discovery, metadata, and invocation with validation.
    /// </summary>
    public class ToolRegistry
    {
        private readonly Dictionary<string, ToolDefinition> _definitions = new Dictionary<string, ToolDefinition>();
        private readonly Dictionary<string, IEditorMcpTool> _implementations = new Dictionary<string, IEditorMcpTool>();

        /// <summary>
        /// Gets the number of registered tools.
        /// </summary>
        public int Count => _definitions.Count;

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
        /// Gets a tool definition by ID.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <returns>The tool definition, or null if not found.</returns>
        public ToolDefinition Get(string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
                return null;

            return _definitions.TryGetValue(toolId, out var definition) ? definition : null;
        }

        /// <summary>
        /// Lists all registered tools, optionally filtered by category and tier.
        /// </summary>
        /// <param name="category">Optional category filter.</param>
        /// <param name="tier">Optional tier filter.</param>
        /// <returns>List of tool definitions matching the filters.</returns>
        public List<ToolDefinition> List(string category = null, string tier = null)
        {
            var query = _definitions.Values.AsEnumerable();

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

            // Check if tool exists
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
        /// Checks if a tool is registered.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <returns>True if the tool is registered, false otherwise.</returns>
        public bool IsRegistered(string toolId)
        {
            return !string.IsNullOrEmpty(toolId) && _definitions.ContainsKey(toolId);
        }

        /// <summary>
        /// Clears all registered tools and implementations.
        /// </summary>
        public void Clear()
        {
            _definitions.Clear();
            _implementations.Clear();
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

