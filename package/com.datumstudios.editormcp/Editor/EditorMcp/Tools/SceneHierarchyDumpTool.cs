using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DatumStudios.EditorMCP.Schemas;

namespace DatumStudios.EditorMCP.Tools
{
    /// <summary>
    /// Tool: scene.hierarchy.dump - Returns the complete GameObject hierarchy for a scene.
    /// </summary>
    public class SceneHierarchyDumpTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the SceneHierarchyDumpTool class.
        /// </summary>
        public SceneHierarchyDumpTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to dump scene hierarchy.
        /// </summary>
        /// <param name="request">The tool invocation request with optional scenePath and includeInactive.</param>
        /// <returns>Scene hierarchy response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            string scenePath = null;
            bool includeInactive = false;

            if (request.Arguments != null)
            {
                if (request.Arguments.TryGetValue("scenePath", out var scenePathObj) && scenePathObj is string)
                {
                    scenePath = (string)scenePathObj;
                }

                if (request.Arguments.TryGetValue("includeInactive", out var includeInactiveObj))
                {
                    if (includeInactiveObj is bool)
                    {
                        includeInactive = (bool)includeInactiveObj;
                    }
                }
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

            var rootObjects = new List<Dictionary<string, object>>();
            var rootGameObjects = scene.GetRootGameObjects();

            // Sort for deterministic ordering
            var sortedRoots = rootGameObjects.OrderBy(go => go.name).ToList();

            foreach (var rootObj in sortedRoots)
            {
                if (includeInactive || rootObj.activeSelf)
                {
                    var node = SerializeGameObject(rootObj, includeInactive);
                    rootObjects.Add(node);
                }
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
                    { "scenePath", scenePath ?? "" },
                    { "rootObjects", rootObjects.ToArray() }
                }
            };

            return response;
        }

        private Dictionary<string, object> SerializeGameObject(GameObject obj, bool includeInactive)
        {
            var components = new List<string>();
            var componentInstanceIds = new List<int>();

            foreach (var component in obj.GetComponents<Component>())
            {
                if (component == null)
                {
                    components.Add("Missing Script");
                    componentInstanceIds.Add(0);
                }
                else
                {
                    components.Add(component.GetType().Name);
                    componentInstanceIds.Add(component.GetInstanceID());
                }
            }

            var node = new Dictionary<string, object>
            {
                { "name", obj.name },
                { "path", GetHierarchyPath(obj) },
                { "active", obj.activeSelf },
                { "components", components.ToArray() },
                { "componentInstanceIds", componentInstanceIds.ToArray() }
            };

            // Recursively serialize children
            var children = new List<Dictionary<string, object>>();
            var childTransforms = new List<Transform>();
            foreach (Transform child in obj.transform)
            {
                childTransforms.Add(child);
            }

            // Sort children for deterministic ordering
            childTransforms = childTransforms.OrderBy(t => t.name).ToList();

            foreach (var childTransform in childTransforms)
            {
                if (includeInactive || childTransform.gameObject.activeSelf)
                {
                    var childNode = SerializeGameObject(childTransform.gameObject, includeInactive);
                    children.Add(childNode);
                }
            }

            if (children.Count > 0)
            {
                node["children"] = children.ToArray();
            }

            return node;
        }

        private string GetHierarchyPath(GameObject obj)
        {
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
                Id = "scene.hierarchy.dump",
                Name = "Dump Scene Hierarchy",
                Description = "Returns the complete GameObject hierarchy for a scene, including components per node and object paths. Cornerstone tool for editor reasoning and structural analysis.",
                Category = "scene",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>
                {
                    {
                        "scenePath",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Path to the scene file (e.g., 'Assets/Scenes/Main.unity'). If not provided, uses currently active scene."
                        }
                    },
                    {
                        "includeInactive",
                        new ToolParameterSchema
                        {
                            Type = "boolean",
                            Required = false,
                            Description = "If true, includes inactive GameObjects in the hierarchy. Default: false.",
                            Default = false
                        }
                    }
                },
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "scenePath",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Path to the scene that was dumped"
                        }
                    },
                    {
                        "rootObjects",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "Root GameObjects in the scene",
                            Items = new ToolOutputSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, ToolOutputSchema>
                                {
                                    {
                                        "name",
                                        new ToolOutputSchema { Type = "string", Description = "GameObject name" }
                                    },
                                    {
                                        "path",
                                        new ToolOutputSchema { Type = "string", Description = "Hierarchy path" }
                                    },
                                    {
                                        "active",
                                        new ToolOutputSchema { Type = "boolean", Description = "Active state" }
                                    },
                                    {
                                        "components",
                                        new ToolOutputSchema
                                        {
                                            Type = "array",
                                            Description = "Component type names",
                                            Items = new ToolOutputSchema { Type = "string" }
                                        }
                                    },
                                    {
                                        "componentInstanceIds",
                                        new ToolOutputSchema
                                        {
                                            Type = "array",
                                            Description = "Component instance IDs",
                                            Items = new ToolOutputSchema { Type = "integer" }
                                        }
                                    },
                                    {
                                        "children",
                                        new ToolOutputSchema
                                        {
                                            Type = "array",
                                            Description = "Child GameObjects (recursive structure)",
                                            Items = new ToolOutputSchema { Type = "object" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                Notes = "Read-only. Reads scene hierarchy only; no GameObjects or components are modified. Large scenes with thousands of objects may take several seconds to process."
            };
        }
    }
}

