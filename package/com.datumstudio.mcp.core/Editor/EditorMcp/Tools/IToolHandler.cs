using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Interface for EditorMCP tools. Each tool provides its definition and can be invoked.
    /// </summary>
    public interface IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition, including schema, metadata, and safety information.
        /// </summary>
        ToolDefinition Definition { get; }

        /// <summary>
        /// Invokes the tool with the given request. Input validation is performed by the registry
        /// before this method is called, so implementations can assume valid inputs.
        /// </summary>
        /// <param name="request">The validated tool invocation request.</param>
        /// <returns>The tool invocation response with output data.</returns>
        ToolInvokeResponse Invoke(ToolInvokeRequest request);
    }
}

