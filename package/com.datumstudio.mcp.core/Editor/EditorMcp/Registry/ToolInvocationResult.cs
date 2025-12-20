using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Registry
{
    /// <summary>
    /// Result of a tool invocation, either success with response or failure with error.
    /// </summary>
    public class ToolInvocationResult
    {
        /// <summary>
        /// Gets whether the invocation was successful.
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// Gets the response if successful, or null if failed.
        /// </summary>
        public ToolInvokeResponse Response { get; private set; }

        /// <summary>
        /// Gets the error if failed, or null if successful.
        /// </summary>
        public EditorMcpError Error { get; private set; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="response">The tool invocation response.</param>
        /// <returns>A successful ToolInvocationResult.</returns>
        public static ToolInvocationResult Success(ToolInvokeResponse response)
        {
            return new ToolInvocationResult
            {
                IsSuccess = true,
                Response = response,
                Error = null
            };
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="error">The error that occurred.</param>
        /// <returns>A failed ToolInvocationResult.</returns>
        public static ToolInvocationResult Failure(EditorMcpError error)
        {
            return new ToolInvocationResult
            {
                IsSuccess = false,
                Response = null,
                Error = error
            };
        }

        private ToolInvocationResult()
        {
        }
    }
}

