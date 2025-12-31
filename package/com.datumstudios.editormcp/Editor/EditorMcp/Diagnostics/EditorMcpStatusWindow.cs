using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DatumStudios.EditorMCP.Registry;
using DatumStudios.EditorMCP.Server;
using DatumStudios.EditorMCP.Schemas;
using DatumStudios.EditorMCP;

namespace DatumStudios.EditorMCP.Diagnostics
{
    /// <summary>
    /// EditorWindow for displaying EditorMCP server status and controls.
    /// </summary>
    public class EditorMcpStatusWindow : EditorWindow
    {
        private static EditorMcpServer _server;
        private Vector2 _scrollPosition;
        private double _lastRefreshTime;
        private readonly float _refreshInterval = 1.0f; // Refresh every second

        /// <summary>
        /// Opens the EditorMCP Status window.
        /// </summary>
        [MenuItem("Window/EditorMCP/Status")]
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
            
            // Subscribe to tool discovery events
            ToolRegistry.OnToolsDiscovered += OnToolsDiscovered;
            
            // Log domain reload event
            Debug.Log($"[EditorMCP] Domain reload detected - Status Window enabled");
        }

        private void OnGUI()
        {
            if (_server == null)
            {
                EditorGUILayout.HelpBox("Server instance not available.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(10);

            // Version & Compatibility
            EditorGUILayout.LabelField("EditorMCP Core", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Version:", _server.ServerVersion);
            
            // Unity Version Compatibility
            var isCompatible = VersionValidator.IsCompatible();
            var compatibilityText = isCompatible ? "Compatible ✓" : $"Incompatible ✗ (Requires {VersionValidator.GetMinimumVersion()}+)";
            var compatibilityColor = isCompatible ? Color.green : Color.red;
            
            var originalColor1 = GUI.color;
            GUI.color = compatibilityColor;
            EditorGUILayout.LabelField("Unity Version:", $"{Application.unityVersion} - {compatibilityText}");
            GUI.color = originalColor1;
            
            if (!isCompatible)
            {
                EditorGUILayout.HelpBox($"EditorMCP requires Unity {VersionValidator.GetMinimumVersion()}+. Please upgrade to Unity 2022.3 LTS or later.", MessageType.Error);
            }
            
            EditorGUILayout.Space(5);

            // Status
            EditorGUILayout.LabelField("Server Status", EditorStyles.boldLabel);
            var statusText = _server.IsRunning ? "Running" : "Stopped";
            var statusColor = _server.IsRunning ? Color.green : Color.gray;
            
            var originalColor2 = GUI.color;
            GUI.color = statusColor;
            EditorGUILayout.LabelField("Status:", statusText);
            GUI.color = originalColor2;

            // Transport Status & Configuration
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Transport Status", EditorStyles.boldLabel);
            
            // Transport type and status
            var transportType = "Stdio"; // STDIO is primary, WebSocket is future
            var transportStatus = _server.IsRunning && _server.TransportHost != null && _server.TransportHost.IsRunning
                ? $"{transportType} ✓"
                : $"{transportType} ✗";
            
            var transportColor = (_server.IsRunning && _server.TransportHost != null && _server.TransportHost.IsRunning) 
                ? Color.green 
                : Color.gray;
            
            var originalColor3 = GUI.color;
            GUI.color = transportColor;
            EditorGUILayout.LabelField("Transport:", transportStatus);
            GUI.color = originalColor3;
            
            // Port Configuration (for future WebSocket support)
            var currentPort = EditorPrefs.GetInt("EditorMcp.Port", 27182);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Port (WebSocket):", GUILayout.Width(150));
            var newPort = EditorGUILayout.IntField(currentPort, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            
            // Validate and apply port change
            if (newPort != currentPort)
            {
                if (newPort >= 27182 && newPort <= 65535)
                {
                    try
                    {
                        _server.ConfigurePort(newPort);
                        if (_server.IsRunning)
                        {
                            EditorGUILayout.HelpBox("Port changed. Restart server to apply changes.", MessageType.Info);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        EditorGUILayout.HelpBox($"Failed to configure port: {ex.Message}", MessageType.Error);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Port must be between 27182 and 65535.", MessageType.Warning);
                }
            }
            
            // Uptime display
            if (_server.IsRunning && _server.TransportHost != null && _server.TransportHost.StartedAt.HasValue)
            {
                var uptime = DateTime.UtcNow - _server.TransportHost.StartedAt.Value;
                EditorGUILayout.LabelField("Uptime:", $"{uptime.TotalSeconds:F1}s");
            }
            
            EditorGUILayout.Space(5);

            // Tool count and breakdown
            if (_server.IsRunning)
            {
                var toolCount = _server.ToolRegistry.Count;
                EditorGUILayout.LabelField("Registered Tools:", toolCount.ToString());
                
                // Enhanced tool breakdown by category and tier
                if (toolCount > 0)
                {
                    var tools = _server.ToolRegistry.List();
                    
                    // Tools by Category
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Tools by Category:", EditorStyles.miniLabel);
                    var categories = tools.GroupBy(t => t.Category ?? "uncategorized")
                                          .OrderBy(g => g.Key);
                    foreach (var categoryGroup in categories)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"  {categoryGroup.Key}:", GUILayout.Width(150));
                        EditorGUILayout.LabelField($"{categoryGroup.Count()} tools", EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    // Tools by Tier
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Tools by Tier:", EditorStyles.miniLabel);
                    var tiers = tools.GroupBy(t => t.Tier ?? "core")
                                     .OrderBy(g => g.Key);
                    foreach (var tierGroup in tiers)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"  {tierGroup.Key}:", GUILayout.Width(150));
                        EditorGUILayout.LabelField($"{tierGroup.Count()} tools", EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    // Discovery Method
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Discovery Method:", EditorStyles.miniLabel);
                    var attributeCount = _server.ToolRegistry.AttributeToolCount;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("  Attribute-based:", GUILayout.Width(150));
                    EditorGUILayout.LabelField($"{attributeCount} tools", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.Space(10);

            // Start/Stop/Restart buttons
            EditorGUILayout.BeginHorizontal();
            
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
                
                if (GUILayout.Button("Restart Server", GUILayout.Height(30)))
                {
                    try
                    {
                        _server.Restart();
                        Debug.Log($"EditorMCP server restarted. Registered {_server.ToolRegistry.Count} tools.");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to restart server: {ex.Message}");
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
            
            EditorGUILayout.EndHorizontal();

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

        private void OnDisable()
        {
            // Unsubscribe from tool discovery events
            ToolRegistry.OnToolsDiscovered -= OnToolsDiscovered;
        }
        
        private void OnToolsDiscovered(int toolCount)
        {
            Debug.Log($"[EditorMCP] Status Window: Tool registry populated with {toolCount} tools");
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

