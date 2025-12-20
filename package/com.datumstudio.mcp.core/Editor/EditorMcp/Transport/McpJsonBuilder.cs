using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DatumStudio.Mcp.Core.Editor.Transport
{
    /// <summary>
    /// Helper class for building JSON strings from dictionaries and objects.
    /// Provides simple JSON serialization for MCP responses (v0.1 implementation).
    /// TODO: Replace with proper JSON library in future versions.
    /// </summary>
    internal static class McpJsonBuilder
    {
        /// <summary>
        /// Builds a JSON string from a dictionary.
        /// </summary>
        public static string BuildJson(Dictionary<string, object> dict)
        {
            if (dict == null)
                return "null";

            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;

            foreach (var kvp in dict)
            {
                if (!first)
                    sb.Append(",");
                first = false;

                sb.Append(JsonEscape(kvp.Key));
                sb.Append(":");
                sb.Append(JsonValue(kvp.Value));
            }

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Builds a JSON string for an MCP response.
        /// </summary>
        public static string BuildResponse(McpResponse response)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"jsonrpc\":\"2.0\"");

            // ID
            if (response.Id != null)
            {
                sb.Append(",\"id\":");
                sb.Append(JsonValue(response.Id));
            }
            else
            {
                sb.Append(",\"id\":null");
            }

            // Result or Error
            if (response.Error != null)
            {
                sb.Append(",\"error\":");
                sb.Append(BuildError(response.Error));
            }
            else if (response.Result != null && response.Result.Count > 0)
            {
                sb.Append(",\"result\":");
                sb.Append(BuildJson(response.Result));
            }

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Builds a JSON string for an MCP error.
        /// </summary>
        private static string BuildError(McpError error)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"code\":{error.Code}");
            sb.Append($",\"message\":{JsonEscape(error.Message)}");

            if (error.Data != null && error.Data.Count > 0)
            {
                sb.Append(",\"data\":");
                sb.Append(BuildJson(error.Data));
            }
            else
            {
                sb.Append(",\"data\":{}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Converts a value to JSON representation.
        /// </summary>
        private static string JsonValue(object value)
        {
            if (value == null)
                return "null";

            if (value is string str)
                return JsonEscape(str);

            if (value is bool b)
                return b ? "true" : "false";

            if (value is int || value is long || value is short || value is byte)
                return value.ToString();

            if (value is float || value is double || value is decimal)
                return value.ToString();

            if (value is Dictionary<string, object> dict)
                return BuildJson(dict);

            if (value is IEnumerable enumerable && !(value is string))
            {
                var sb = new StringBuilder();
                sb.Append("[");
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (!first)
                        sb.Append(",");
                    first = false;
                    sb.Append(JsonValue(item));
                }
                sb.Append("]");
                return sb.ToString();
            }

            // Fallback: convert to string
            return JsonEscape(value.ToString());
        }

        /// <summary>
        /// Escapes a string for JSON.
        /// </summary>
        private static string JsonEscape(string str)
        {
            if (str == null)
                return "null";

            var sb = new StringBuilder();
            sb.Append("\"");

            foreach (var c in str)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append($"\\u{((int)c):X4}");
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            sb.Append("\"");
            return sb.ToString();
        }
    }
}

