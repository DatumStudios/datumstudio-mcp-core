using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DatumStudio.Mcp.Core.Editor.Registry;
using DatumStudio.Mcp.Core.Editor.Server;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Diagnostics
{
    /// <summary>
    /// EditorWindow for displaying EditorMCP server status and controls.
    /// </summary>
    public class EditorMcpStatusWindow : EditorWindow
    {
        private static EditorMcpServer _server;
        private Vector2 _scrollPosition;

        /// <summary>
        /// Opens the EditorMCP Status window.
        /// </summary>
        [MenuItem("Window/DatumStudio/EditorMCP Status")]
        public static void ShowWindow()
        {
            var window = GetWindow<EditorMcpStatusWindow>("EditorMCP Status");
            window.minSize = new Vector2(300, 200);
        }

        private void OnEnable()
        {
            // Initialize server instance if needed
            if (_server == null)
            {
                _server = new EditorMcpServer();
            }
        }

        private void OnGUI()
        {
            if (_server == null)
            {
                EditorGUILayout.HelpBox("Server instance not available.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(10);

            // Version
            EditorGUILayout.LabelField("EditorMCP Core", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Version:", _server.ServerVersion);
            EditorGUILayout.Space(5);

            // Status
            EditorGUILayout.LabelField("Server Status", EditorStyles.boldLabel);
            var statusText = _server.IsRunning ? "Running" : "Stopped";
            var statusColor = _server.IsRunning ? Color.green : Color.gray;
            
            var originalColor = GUI.color;
            GUI.color = statusColor;
            EditorGUILayout.LabelField("Status:", statusText);
            GUI.color = originalColor;

            // Transport info
            if (_server.IsRunning && _server.TransportHost != null)
            {
                var transport = _server.TransportHost;
                EditorGUILayout.LabelField("Transport:", transport.IsRunning ? "Active" : "Inactive");
                if (transport.StartedAt.HasValue)
                {
                    var uptime = DateTime.UtcNow - transport.StartedAt.Value;
                    EditorGUILayout.LabelField("Uptime:", $"{uptime.TotalSeconds:F1}s");
                }
            }
            EditorGUILayout.Space(5);

            // Tool count
            if (_server.IsRunning)
            {
                EditorGUILayout.LabelField("Registered Tools:", _server.ToolRegistry.Count.ToString());
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.Space(10);

            // Start/Stop button
            if (_server.IsRunning)
            {
                if (GUILayout.Button("Stop Server", GUILayout.Height(30)))
                {
                    try
                    {
                        _server.Stop();
                        Debug.Log("EditorMCP server stopped.");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to stop server: {ex.Message}");
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Start Server", GUILayout.Height(30)))
                {
                    try
                    {
                        _server.Start();
                        Debug.Log($"EditorMCP server started. Registered {_server.ToolRegistry.Count} tools.");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to start server: {ex.Message}");
                    }
                }
            }

            EditorGUILayout.Space(10);

            // Copy tool list button
            if (_server.IsRunning && _server.ToolRegistry.Count > 0)
            {
                if (GUILayout.Button("Copy Tool List JSON", GUILayout.Height(25)))
                {
                    CopyToolListToClipboard();
                }
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button("Copy Tool List JSON (Server must be running)", GUILayout.Height(25));
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space(10);

            // Tool list preview (read-only)
            if (_server.IsRunning && _server.ToolRegistry.Count > 0)
            {
                EditorGUILayout.LabelField("Registered Tools:", EditorStyles.boldLabel);
                
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
                
                var tools = _server.ToolRegistry.List().OrderBy(t => t.Id).ToList();
                foreach (var tool in tools)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(tool.Id, GUILayout.Width(200));
                    EditorGUILayout.LabelField(tool.Category, GUILayout.Width(100));
                    EditorGUILayout.LabelField(tool.SafetyLevel.ToString(), GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
            }
        }

        private void CopyToolListToClipboard()
        {
            try
            {
                var tools = _server.ToolRegistry.List().OrderBy(t => t.Id).ToList();
                
                // Build JSON manually for better control
                var jsonBuilder = new System.Text.StringBuilder();
                jsonBuilder.AppendLine("{");
                jsonBuilder.AppendLine($"  \"serverVersion\": \"{_server.ServerVersion}\",");
                jsonBuilder.AppendLine($"  \"count\": {tools.Count},");
                jsonBuilder.AppendLine("  \"tools\": [");
                
                for (int i = 0; i < tools.Count; i++)
                {
                    var tool = tools[i];
                    var isLast = i == tools.Count - 1;
                    
                    jsonBuilder.AppendLine("    {");
                    jsonBuilder.AppendLine($"      \"id\": \"{EscapeJson(tool.Id)}\",");
                    jsonBuilder.AppendLine($"      \"name\": \"{EscapeJson(tool.Name)}\",");
                    jsonBuilder.AppendLine($"      \"description\": \"{EscapeJson(tool.Description)}\",");
                    jsonBuilder.AppendLine($"      \"category\": \"{EscapeJson(tool.Category)}\",");
                    jsonBuilder.AppendLine($"      \"safetyLevel\": \"{tool.SafetyLevel}\",");
                    jsonBuilder.AppendLine($"      \"tier\": \"{EscapeJson(tool.Tier)}\"");
                    jsonBuilder.Append(isLast ? "    }" : "    },");
                    jsonBuilder.AppendLine();
                }
                
                jsonBuilder.AppendLine("  ]");
                jsonBuilder.AppendLine("}");
                
                var jsonString = jsonBuilder.ToString();
                EditorGUIUtility.systemCopyBuffer = jsonString;
                
                Debug.Log($"Copied {tools.Count} tools to clipboard.");
                EditorUtility.DisplayDialog("Copied", $"Copied {tools.Count} tools to clipboard.", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to copy tool list: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to copy tool list: {ex.Message}", "OK");
            }
        }

        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            
            return value.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\n", "\\n")
                       .Replace("\r", "\\r")
                       .Replace("\t", "\\t");
        }
    }
}

