using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Tool: editor.selection.info - Returns information about currently selected objects and assets in the Unity Editor.
    /// </summary>
    public class EditorSelectionInfoTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the EditorSelectionInfoTool class.
        /// </summary>
        public EditorSelectionInfoTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to return selection information.
        /// </summary>
        /// <param name="request">The tool invocation request (no arguments required).</param>
        /// <returns>Selection information response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            var selectedObjects = new List<Dictionary<string, object>>();
            var selectedGameObjects = Selection.gameObjects;
            var selectedAssets = Selection.objects.Where(obj => !(obj is GameObject)).ToList();

            // Process selected GameObjects
            if (selectedGameObjects != null && selectedGameObjects.Length > 0)
            {
                // Sort for deterministic ordering
                var sortedGameObjects = selectedGameObjects.OrderBy(go => GetHierarchyPath(go)).ToList();

                foreach (var go in sortedGameObjects)
                {
                    var components = new List<string>();
                    foreach (var component in go.GetComponents<Component>())
                    {
                        if (component == null)
                        {
                            components.Add("Missing Script");
                        }
                        else
                        {
                            components.Add(component.GetType().Name);
                        }
                    }

                    selectedObjects.Add(new Dictionary<string, object>
                    {
                        { "name", go.name },
                        { "type", "GameObject" },
                        { "instanceId", go.GetInstanceID() },
                        { "path", GetHierarchyPath(go) },
                        { "components", components.ToArray() }
                    });
                }
            }

            // Process selected assets (non-GameObject objects)
            if (selectedAssets != null && selectedAssets.Count > 0)
            {
                // Sort for deterministic ordering
                var sortedAssets = selectedAssets.OrderBy(obj => obj.name).ToList();

                foreach (var asset in sortedAssets)
                {
                    var assetPath = AssetDatabase.GetAssetPath(asset);
                    // Guard: Only process assets in Assets/ folder (never touch Packages/)
                    // Skip package assets to avoid "no meta file" errors
                    if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/"))
                    {
                        // Include asset info but mark as package asset
                        selectedObjects.Add(new Dictionary<string, object>
                        {
                            { "name", asset.name },
                            { "type", asset.GetType().Name },
                            { "instanceId", asset.GetInstanceID() },
                            { "path", assetPath ?? "" },
                            { "guid", "" },
                            { "note", "Package asset (not in Assets/ folder)" }
                        });
                        continue;
                    }

                    var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

                    selectedObjects.Add(new Dictionary<string, object>
                    {
                        { "name", asset.name },
                        { "type", asset.GetType().Name },
                        { "instanceId", asset.GetInstanceID() },
                        { "path", assetPath ?? "" },
                        { "guid", assetGuid ?? "" }
                    });
                }
            }

            // Get active scene
            string activeScenePath = "";
            try
            {
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    activeScenePath = activeScene.path ?? "";
                }
            }
            catch
            {
                // Best-effort: if we can't get active scene, leave empty
            }

            // Get active GameObject
            Dictionary<string, object> activeGameObject = null;
            var activeGO = Selection.activeGameObject;
            if (activeGO != null)
            {
                activeGameObject = new Dictionary<string, object>
                {
                    { "name", activeGO.name },
                    { "path", GetHierarchyPath(activeGO) }
                };
            }

            var output = new Dictionary<string, object>
            {
                { "selectedObjects", selectedObjects.ToArray() },
                { "activeScene", activeScenePath }
            };

            if (activeGameObject != null)
            {
                output["activeGameObject"] = activeGameObject;
            }

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = output
            };

            return response;
        }

        private string GetHierarchyPath(GameObject obj)
        {
            if (obj == null)
                return "";

            var path = obj.name;
            var parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "editor.selection.info",
                Name = "Editor Selection Info",
                Description = "Returns information about currently selected objects and assets in the Unity Editor. Bridges human and AI workflows by providing current editor context.",
                Category = "editor",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>(),
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "selectedObjects",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "List of selected objects and assets",
                            Items = new ToolOutputSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, ToolOutputSchema>
                                {
                                    {
                                        "name",
                                        new ToolOutputSchema { Type = "string", Description = "Object name" }
                                    },
                                    {
                                        "type",
                                        new ToolOutputSchema { Type = "string", Description = "Object type (e.g., 'GameObject', 'Material', 'Texture2D')" }
                                    },
                                    {
                                        "instanceId",
                                        new ToolOutputSchema { Type = "integer", Description = "Unity instance ID" }
                                    },
                                    {
                                        "path",
                                        new ToolOutputSchema { Type = "string", Description = "Hierarchy path (for GameObjects) or asset path (for assets)" }
                                    },
                                    {
                                        "components",
                                        new ToolOutputSchema
                                        {
                                            Type = "array",
                                            Description = "Component type names (for GameObjects only)",
                                            Items = new ToolOutputSchema { Type = "string" }
                                        }
                                    },
                                    {
                                        "guid",
                                        new ToolOutputSchema { Type = "string", Description = "Asset GUID (for assets only)" }
                                    }
                                }
                            }
                        }
                    },
                    {
                        "activeScene",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Path to the currently active scene"
                        }
                    },
                    {
                        "activeGameObject",
                        new ToolOutputSchema
                        {
                            Type = "object",
                            Description = "Currently active GameObject (if any)",
                            Properties = new Dictionary<string, ToolOutputSchema>
                            {
                                {
                                    "name",
                                    new ToolOutputSchema { Type = "string", Description = "GameObject name" }
                                },
                                {
                                    "path",
                                    new ToolOutputSchema { Type = "string", Description = "Hierarchy path" }
                                }
                            }
                        }
                    }
                },
                Notes = "Read-only. Reads current editor selection state only; selection is not modified."
            };
        }
    }
}

