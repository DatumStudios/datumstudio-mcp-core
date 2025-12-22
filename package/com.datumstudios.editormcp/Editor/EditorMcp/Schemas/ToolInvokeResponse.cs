using System.Collections.Generic;

namespace DatumStudios.EditorMCP.Schemas
{
    /// <summary>
    /// Represents the response from a tool invocation.
    /// </summary>
    public class ToolInvokeResponse
    {
        /// <summary>
        /// The ID of the tool that was executed.
        /// </summary>
        public string Tool { get; set; }

        /// <summary>
        /// Tool-specific output data.
        /// </summary>
        public Dictionary<string, object> Output { get; set; }

        /// <summary>
        /// Optional diagnostics array containing warnings, notes, or performance information.
        /// Used to communicate best-effort limitations, partial results, or performance bounds.
        /// </summary>
        public string[] Diagnostics { get; set; }

        /// <summary>
        /// Initializes a new instance of the ToolInvokeResponse class.
        /// </summary>
        public ToolInvokeResponse()
        {
            Output = new Dictionary<string, object>();
            Diagnostics = null;
        }
    }
}

