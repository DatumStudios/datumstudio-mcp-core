using System;
using System.Collections.Generic;
using UnityEngine;

namespace DatumStudios.EditorMCP.Transport
{
    /// <summary>
    /// Represents an MCP response envelope (JSON-RPC 2.0 format).
    /// </summary>
    [Serializable]
    public class McpResponse
    {
        /// <summary>
        /// JSON-RPC version. Must be "2.0".
        /// </summary>
        public string jsonrpc = "2.0";

        /// <summary>
        /// Request identifier from the corresponding request (as string for JsonUtility compatibility).
        /// </summary>
        public string id;

        /// <summary>
        /// Result object (for success responses) - stored as JSON string for serialization.
        /// </summary>
        public string resultJson;

        /// <summary>
        /// Error object (for error responses).
        /// </summary>
        public McpError error;

        /// <summary>
        /// Gets or sets the JSON-RPC version.
        /// </summary>
        public string JsonRpc
        {
            get => jsonrpc;
            set => jsonrpc = value ?? "2.0";
        }

        /// <summary>
        /// Gets or sets the request ID.
        /// </summary>
        public object Id
        {
            get
            {
                if (string.IsNullOrEmpty(id))
                    return null;
                
                if (long.TryParse(id, out long longValue))
                    return longValue;
                if (double.TryParse(id, out double doubleValue))
                    return doubleValue;
                
                return id;
            }
            set
            {
                id = value?.ToString();
            }
        }

        /// <summary>
        /// Gets or sets the result dictionary.
        /// </summary>
        public Dictionary<string, object> Result
        {
            get
            {
                if (string.IsNullOrEmpty(resultJson))
                    return new Dictionary<string, object>();
                
                // Parse resultJson - simplified for v0.1
                // TODO: Use proper JSON parser in future versions
                return new Dictionary<string, object>();
            }
            set
            {
                // Store as JSON string - simplified for v0.1
                // TODO: Use proper JSON serialization in future versions
                resultJson = JsonUtility.ToJson(value);
            }
        }

        /// <summary>
        /// Gets or sets the error object.
        /// </summary>
        public McpError Error
        {
            get => error;
            set => error = value;
        }

        /// <summary>
        /// Initializes a new instance of the McpResponse class.
        /// </summary>
        public McpResponse()
        {
            jsonrpc = "2.0";
        }
    }

    /// <summary>
    /// Represents an MCP error object.
    /// </summary>
    [Serializable]
    public class McpError
    {
        /// <summary>
        /// JSON-RPC error code.
        /// </summary>
        public int code;

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string message;

        /// <summary>
        /// Additional error data as JSON string.
        /// </summary>
        public string dataJson;

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public int Code
        {
            get => code;
            set => code = value;
        }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message
        {
            get => message;
            set => message = value;
        }

        /// <summary>
        /// Gets or sets the error data dictionary.
        /// </summary>
        public Dictionary<string, object> Data
        {
            get
            {
                if (string.IsNullOrEmpty(dataJson))
                    return new Dictionary<string, object>();
                
                // Parse dataJson - simplified for v0.1
                return new Dictionary<string, object>();
            }
            set
            {
                // Store as JSON string - simplified for v0.1
                dataJson = JsonUtility.ToJson(value);
            }
        }
    }
}

