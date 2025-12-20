using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DatumStudio.Mcp.Core.Editor.Transport
{
    /// <summary>
    /// Helper class for parsing MCP JSON envelopes. Provides simple JSON parsing
    /// for the specific structures used in MCP protocol (v0.1 implementation).
    /// TODO: Replace with proper JSON library in future versions for full Dictionary&lt;string, object&gt; support.
    /// </summary>
    internal static class McpJsonHelper
    {
        /// <summary>
        /// Parses a JSON string into a McpRequest object.
        /// </summary>
        public static McpRequest ParseRequest(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                // Use JsonUtility to parse basic structure
                var request = JsonUtility.FromJson<McpRequest>(json);
                if (request == null)
                    return null;

                // Extract params manually (JsonUtility doesn't support Dictionary<string, object>)
                request.paramsJson = ExtractJsonField(json, "params");
                
                return request;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts a JSON field value as a string.
        /// </summary>
        private static string ExtractJsonField(string json, string fieldName)
        {
            try
            {
                var searchKey = $"\"{fieldName}\":";
                var startIndex = json.IndexOf(searchKey, StringComparison.Ordinal);
                if (startIndex < 0)
                    return null;

                startIndex += searchKey.Length;
                
                // Skip whitespace
                while (startIndex < json.Length && char.IsWhiteSpace(json[startIndex]))
                    startIndex++;

                if (startIndex >= json.Length)
                    return null;

                // Find the start of the value
                var valueStart = startIndex;
                if (json[valueStart] == '{')
                {
                    // Object - find matching brace
                    var braceCount = 0;
                    var endIndex = valueStart;
                    for (int i = valueStart; i < json.Length; i++)
                    {
                        if (json[i] == '{') braceCount++;
                        if (json[i] == '}') braceCount--;
                        if (braceCount == 0)
                        {
                            endIndex = i + 1;
                            break;
                        }
                    }
                    return json.Substring(valueStart, endIndex - valueStart);
                }
                else if (json[valueStart] == '[')
                {
                    // Array - find matching bracket
                    var bracketCount = 0;
                    var endIndex = valueStart;
                    for (int i = valueStart; i < json.Length; i++)
                    {
                        if (json[i] == '[') bracketCount++;
                        if (json[i] == ']') bracketCount--;
                        if (bracketCount == 0)
                        {
                            endIndex = i + 1;
                            break;
                        }
                    }
                    return json.Substring(valueStart, endIndex - valueStart);
                }
                else if (json[valueStart] == '"')
                {
                    // String - find closing quote
                    var endIndex = json.IndexOf('"', valueStart + 1);
                    if (endIndex > 0)
                        return json.Substring(valueStart, endIndex - valueStart + 1);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parses a JSON params object into a dictionary (simplified for v0.1).
        /// </summary>
        public static Dictionary<string, object> ParseParams(string paramsJson)
        {
            var result = new Dictionary<string, object>();
            
            if (string.IsNullOrEmpty(paramsJson) || paramsJson.Trim() == "null")
                return result;

            try
            {
                // Remove outer braces
                var trimmed = paramsJson.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
                }

                // Simple parsing for common MCP params structures
                // This is a best-effort implementation for v0.1
                var parts = SplitJsonFields(trimmed);
                foreach (var part in parts)
                {
                    var colonIndex = part.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var key = part.Substring(0, colonIndex).Trim().Trim('"', '\'');
                        var value = part.Substring(colonIndex + 1).Trim();
                        
                        // Remove quotes from string values
                        if (value.StartsWith("\"") && value.EndsWith("\""))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }
                        
                        result[key] = value;
                    }
                }
            }
            catch
            {
                // Best-effort: return what we parsed
            }

            return result;
        }

        /// <summary>
        /// Splits JSON fields, handling nested objects and arrays.
        /// </summary>
        private static List<string> SplitJsonFields(string json)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var depth = 0;
            var inString = false;
            var escapeNext = false;

            for (int i = 0; i < json.Length; i++)
            {
                var c = json[i];

                if (escapeNext)
                {
                    current.Append(c);
                    escapeNext = false;
                    continue;
                }

                if (c == '\\')
                {
                    escapeNext = true;
                    current.Append(c);
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    current.Append(c);
                    continue;
                }

                if (inString)
                {
                    current.Append(c);
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    depth++;
                    current.Append(c);
                    continue;
                }

                if (c == '}' || c == ']')
                {
                    depth--;
                    current.Append(c);
                    continue;
                }

                if (c == ',' && depth == 0)
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
            {
                fields.Add(current.ToString().Trim());
            }

            return fields;
        }
    }
}

