using System.Collections.Generic;

namespace DatumStudio.Mcp.Core.Editor.Schemas
{
    /// <summary>
    /// Represents a request to invoke a tool with specific arguments.
    /// </summary>
    public class ToolInvokeRequest
    {
        /// <summary>
        /// The ID of the tool to execute.
        /// </summary>
        public string Tool { get; set; }

        /// <summary>
        /// Tool-specific input parameters.
        /// Key is parameter name, value is the parameter value.
        /// </summary>
        public Dictionary<string, object> Arguments { get; set; }

        /// <summary>
        /// Initializes a new instance of the ToolInvokeRequest class.
        /// </summary>
        public ToolInvokeRequest()
        {
            Arguments = new Dictionary<string, object>();
        }
    }
}

