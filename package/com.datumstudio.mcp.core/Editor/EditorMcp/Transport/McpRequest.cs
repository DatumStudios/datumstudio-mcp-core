using System;
using System.Collections.Generic;
using UnityEngine;

namespace DatumStudio.Mcp.Core.Editor.Transport
{
    /// <summary>
    /// Represents an MCP request envelope (JSON-RPC 2.0 format).
    /// </summary>
    [Serializable]
    public class McpRequest
    {
        /// <summary>
        /// JSON-RPC version. Must be "2.0".
        /// </summary>
        public string jsonrpc;

        /// <summary>
        /// Request identifier (string or number).
        /// Note: JsonUtility doesn't support object directly, so we'll handle this as string/number separately.
        /// </summary>
        public string id;

        /// <summary>
        /// MCP method name (e.g., "tools/call" - canonical for tool invocations, "tools/list", "tools/describe", "server/info" - legacy).
        /// </summary>
        public string method;

        /// <summary>
        /// Method-specific parameters as JSON string (will be parsed separately).
        /// JsonUtility doesn't support Dictionary&lt;string, object&gt; directly.
        /// </summary>
        public string paramsJson;

        /// <summary>
        /// Gets the JSON-RPC version.
        /// </summary>
        public string JsonRpc => jsonrpc;

        /// <summary>
        /// Gets the request ID as an object (string or number).
        /// </summary>
        public object Id
        {
            get
            {
                if (string.IsNullOrEmpty(id))
                    return null;
                
                // Try to parse as number, otherwise return as string
                if (long.TryParse(id, out long longValue))
                    return longValue;
                if (double.TryParse(id, out double doubleValue))
                    return doubleValue;
                
                return id;
            }
        }

        /// <summary>
        /// Gets the method name.
        /// </summary>
        public string Method => method;

        /// <summary>
        /// Gets the parameters as a dictionary (parsed from paramsJson).
        /// </summary>
        public Dictionary<string, object> Params => McpJsonHelper.ParseParams(paramsJson);
    }
}

