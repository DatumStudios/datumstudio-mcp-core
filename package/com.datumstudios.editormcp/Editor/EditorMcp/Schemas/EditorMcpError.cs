using System.Collections.Generic;

namespace DatumStudios.EditorMCP.Schemas
{
    /// <summary>
    /// Represents an error that occurred during tool execution or request processing.
    /// </summary>
    public class EditorMcpError
    {
        /// <summary>
        /// JSON-RPC error code.
        /// Standard codes: -32700 (Parse), -32600 (Invalid Request), -32601 (Method not found),
        /// -32602 (Invalid params), -32603 (Internal error).
        /// EditorMCP extended codes: -32000 (Tool execution), -32001 (Tool not found),
        /// -32002 (Invalid arguments), -32003 (Timeout), -32004 (Unity Editor), -32005 (Permission).
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Additional error information.
        /// </summary>
        public EditorMcpErrorData Data { get; set; }

        /// <summary>
        /// Initializes a new instance of the EditorMcpError class.
        /// </summary>
        public EditorMcpError()
        {
            Data = new EditorMcpErrorData();
        }
    }

    /// <summary>
    /// Additional error information.
    /// </summary>
    public class EditorMcpErrorData
    {
        /// <summary>
        /// The tool ID that caused the error (if applicable).
        /// </summary>
        public string Tool { get; set; }

        /// <summary>
        /// Error type: "validation", "execution", "unity", "permission", "timeout", "not_found".
        /// </summary>
        public string ErrorType { get; set; }

        /// <summary>
        /// Tool-specific error details.
        /// </summary>
        public Dictionary<string, object> Details { get; set; }

        /// <summary>
        /// Initializes a new instance of the EditorMcpErrorData class.
        /// </summary>
        public EditorMcpErrorData()
        {
            Details = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Standard JSON-RPC error codes.
    /// </summary>
    public static class JsonRpcErrorCodes
    {
        /// <summary>Parse error.</summary>
        public const int ParseError = -32700;

        /// <summary>Invalid Request.</summary>
        public const int InvalidRequest = -32600;

        /// <summary>Method not found.</summary>
        public const int MethodNotFound = -32601;

        /// <summary>Invalid params.</summary>
        public const int InvalidParams = -32602;

        /// <summary>Internal error.</summary>
        public const int InternalError = -32603;
    }

    /// <summary>
    /// EditorMCP extended error codes.
    /// </summary>
    public static class EditorMcpErrorCodes
    {
        /// <summary>Tool execution error.</summary>
        public const int ToolExecutionError = -32000;

        /// <summary>Tool not found.</summary>
        public const int ToolNotFound = -32001;

        /// <summary>Invalid tool arguments.</summary>
        public const int InvalidToolArguments = -32002;

        /// <summary>Tool execution timeout.</summary>
        public const int ToolExecutionTimeout = -32003;

        /// <summary>Unity Editor error.</summary>
        public const int UnityEditorError = -32004;

        /// <summary>Permission denied (tier restriction).</summary>
        public const int PermissionDenied = -32005;
    }

    /// <summary>
    /// Error type constants.
    /// </summary>
    public static class ErrorTypes
    {
        /// <summary>Input validation failed.</summary>
        public const string Validation = "validation";

        /// <summary>Tool execution failed.</summary>
        public const string Execution = "execution";

        /// <summary>Unity Editor error.</summary>
        public const string Unity = "unity";

        /// <summary>Tier or permission restriction.</summary>
        public const string Permission = "permission";

        /// <summary>Operation timed out.</summary>
        public const string Timeout = "timeout";

        /// <summary>Resource not found.</summary>
        public const string NotFound = "not_found";
    }
}

