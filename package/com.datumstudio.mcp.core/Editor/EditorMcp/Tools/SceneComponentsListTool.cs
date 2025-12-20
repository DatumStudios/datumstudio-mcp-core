using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Tool: scene.components.list - Returns all components attached to a specific GameObject.
    /// </summary>
    public class SceneComponentsListTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the SceneComponentsListTool class.
        /// </summary>
        public SceneComponentsListTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to list components on a GameObject.
        /// </summary>
        /// <param name="request">The tool invocation request with gameObjectPath and optional scenePath.</param>
        /// <returns>Components list response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            string scenePath = null;
            string gameObjectPath = null;

            if (request.Arguments != null)
            {
                if (request.Arguments.TryGetValue("scenePath", out var scenePathObj) && scenePathObj is string)
                {
                    scenePath = (string)scenePathObj;
                }

                if (request.Arguments.TryGetValue("gameObjectPath", out var gameObjectPathObj) && gameObjectPathObj is string)
                {
                    gameObjectPath = (string)gameObjectPathObj;
                }
            }

            if (string.IsNullOrEmpty(gameObjectPath))
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", "gameObjectPath is required" }
                    }
                };
            }

            UnityEngine.SceneManagement.Scene scene;
            bool sceneWasOpen = false;

            if (string.IsNullOrEmpty(scenePath))
            {
                // Use currently active scene
                scene = EditorSceneManager.GetActiveScene();
                scenePath = scene.path;
            }
            else
            {
                // Check if scene is already open
                scene = EditorSceneManager.GetSceneByPath(scenePath);
                if (scene.IsValid() && scene.isLoaded)
                {
                    sceneWasOpen = true;
                }
                else
                {
                    // Open specified scene (additively to preserve current scene)
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }
            }

            // Find GameObject by path
            GameObject targetObject = null;
            var rootObjects = scene.GetRootGameObjects();
            foreach (var rootObj in rootObjects)
            {
                targetObject = FindGameObjectByPath(rootObj, gameObjectPath);
                if (targetObject != null)
                    break;
            }

            var components = new List<Dictionary<string, object>>();

            if (targetObject != null)
            {
                var allComponents = targetObject.GetComponents<Component>();
                for (int i = 0; i < allComponents.Length; i++)
                {
                    var component = allComponents[i];
                    if (component == null)
                    {
                        components.Add(new Dictionary<string, object>
                        {
                            { "type", "Missing Script" },
                            { "instanceId", 0 },
                            { "index", i }
                        });
                    }
                    else
                    {
                        components.Add(new Dictionary<string, object>
                        {
                            { "type", component.GetType().Name },
                            { "instanceId", component.GetInstanceID() },
                            { "index", i },
                            { "fullTypeName", component.GetType().FullName }
                        });
                    }
                }
            }
            else
            {
                // Clean up if we opened a scene
                if (!string.IsNullOrEmpty(scenePath) && !sceneWasOpen && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, false);
                }

                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", $"GameObject with path '{gameObjectPath}' not found in scene" }
                    }
                };
            }

            // Clean up if we opened a scene
            if (!string.IsNullOrEmpty(scenePath) && !sceneWasOpen && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, false);
            }

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = new Dictionary<string, object>
                {
                    { "gameObjectPath", gameObjectPath },
                    { "scenePath", scenePath ?? "" },
                    { "components", components.ToArray() }
                }
            };

            return response;
        }

        private GameObject FindGameObjectByPath(GameObject root, string path)
        {
            if (root.name == path)
            {
                return root;
            }

            var pathParts = path.Split('/');
            if (pathParts.Length == 0)
                return null;

            if (pathParts[0] != root.name)
                return null;

            if (pathParts.Length == 1)
                return root;

            // Navigate down the hierarchy
            Transform current = root.transform;
            for (int i = 1; i < pathParts.Length; i++)
            {
                var child = current.Find(pathParts[i]);
                if (child == null)
                {
                    // Try direct child search
                    bool found = false;
                    foreach (Transform c in current)
                    {
                        if (c.name == pathParts[i])
                        {
                            current = c;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        return null;
                }
                else
                {
                    current = child;
                }
            }

            return current.gameObject;
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "scene.components.list",
                Name = "List Scene Components",
                Description = "Returns all components attached to a specific GameObject, including serialized field names. Enables safe inspection before any potential edits.",
                Category = "scene",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>
                {
                    {
                        "gameObjectPath",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = true,
                            Description = "Hierarchy path to the GameObject (e.g., 'Canvas/Panel/Button')"
                        }
                    },
                    {
                        "scenePath",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Path to the scene file. If not provided, uses currently active scene."
                        }
                    }
                },
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "gameObjectPath",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Hierarchy path to the GameObject"
                        }
                    },
                    {
                        "scenePath",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Path to the scene"
                        }
                    },
                    {
                        "components",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "List of components",
                            Items = new ToolOutputSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, ToolOutputSchema>
                                {
                                    {
                                        "type",
                                        new ToolOutputSchema { Type = "string", Description = "Component type name" }
                                    },
                                    {
                                        "instanceId",
                                        new ToolOutputSchema { Type = "integer", Description = "Component instance ID" }
                                    },
                                    {
                                        "index",
                                        new ToolOutputSchema { Type = "integer", Description = "Component index" }
                                    },
                                    {
                                        "fullTypeName",
                                        new ToolOutputSchema { Type = "string", Description = "Full type name (namespace included)" }
                                    }
                                }
                            }
                        }
                    }
                },
                Notes = "Read-only. Reads component data only; no components or properties are modified."
            };
        }
    }
}

