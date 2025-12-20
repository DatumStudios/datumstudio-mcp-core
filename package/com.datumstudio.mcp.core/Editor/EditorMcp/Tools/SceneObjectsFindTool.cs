using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Tool: scene.objects.find - Finds GameObjects in a scene matching specified criteria.
    /// </summary>
    public class SceneObjectsFindTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the SceneObjectsFindTool class.
        /// </summary>
        public SceneObjectsFindTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to find objects in a scene.
        /// </summary>
        /// <param name="request">The tool invocation request with search criteria.</param>
        /// <returns>Matching objects response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            string scenePath = null;
            string nameContains = null;
            string tag = null;
            int? layer = null;
            string componentType = null;

            if (request.Arguments != null)
            {
                if (request.Arguments.TryGetValue("scenePath", out var scenePathObj) && scenePathObj is string)
                {
                    scenePath = (string)scenePathObj;
                }

                if (request.Arguments.TryGetValue("nameContains", out var nameContainsObj) && nameContainsObj is string)
                {
                    nameContains = (string)nameContainsObj;
                }

                if (request.Arguments.TryGetValue("tag", out var tagObj) && tagObj is string)
                {
                    tag = (string)tagObj;
                }

                if (request.Arguments.TryGetValue("layer", out var layerObj))
                {
                    if (layerObj is int)
                    {
                        layer = (int)layerObj;
                    }
                    else if (layerObj is long)
                    {
                        layer = (int)(long)layerObj;
                    }
                }

                if (request.Arguments.TryGetValue("componentType", out var componentTypeObj) && componentTypeObj is string)
                {
                    componentType = (string)componentTypeObj;
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

            var allObjects = scene.GetRootGameObjects()
                .SelectMany(go => GetAllGameObjects(go))
                .ToList();

            var matches = new List<Dictionary<string, object>>();

            foreach (var obj in allObjects)
            {
                if (MatchesCriteria(obj, nameContains, tag, layer, componentType))
                {
                    matches.Add(SerializeGameObject(obj));
                }
            }

            // Sort for deterministic ordering
            matches = matches.OrderBy(m => m["path"] as string).ToList();

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
                    { "matches", matches.ToArray() }
                }
            };

            return response;
        }

        private IEnumerable<GameObject> GetAllGameObjects(GameObject root)
        {
            yield return root;
            foreach (Transform child in root.transform)
            {
                foreach (var childObj in GetAllGameObjects(child.gameObject))
                {
                    yield return childObj;
                }
            }
        }

        private bool MatchesCriteria(GameObject obj, string nameContains, string tag, int? layer, string componentType)
        {
            if (!string.IsNullOrEmpty(nameContains))
            {
                if (!obj.name.Contains(nameContains))
                    return false;
            }

            if (!string.IsNullOrEmpty(tag))
            {
                if (!obj.CompareTag(tag))
                    return false;
            }

            if (layer.HasValue)
            {
                if (obj.layer != layer.Value)
                    return false;
            }

            if (!string.IsNullOrEmpty(componentType))
            {
                var hasComponent = false;
                foreach (var component in obj.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == componentType)
                    {
                        hasComponent = true;
                        break;
                    }
                }
                if (!hasComponent)
                    return false;
            }

            return true;
        }

        private Dictionary<string, object> SerializeGameObject(GameObject obj)
        {
            var components = new List<string>();
            foreach (var component in obj.GetComponents<Component>())
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

            return new Dictionary<string, object>
            {
                { "name", obj.name },
                { "path", GetHierarchyPath(obj) },
                { "instanceId", obj.GetInstanceID() },
                { "components", components.ToArray() },
                { "tag", obj.tag },
                { "layer", obj.layer }
            };
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
                Id = "scene.objects.find",
                Name = "Find Scene Objects",
                Description = "Finds GameObjects in a scene matching specified criteria (component type, name pattern, tag, layer). Composable and extremely useful for targeted inspection.",
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
                            Description = "Path to the scene file. If not provided, uses currently active scene."
                        }
                    },
                    {
                        "nameContains",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Filter by GameObject name containing this string (case-sensitive)"
                        }
                    },
                    {
                        "tag",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Filter by tag"
                        }
                    },
                    {
                        "layer",
                        new ToolParameterSchema
                        {
                            Type = "integer",
                            Required = false,
                            Description = "Filter by layer index"
                        }
                    },
                    {
                        "componentType",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Filter by component type name (e.g., 'Rigidbody', 'Camera')"
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
                            Description = "Path to the scene that was searched"
                        }
                    },
                    {
                        "matches",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "Matching GameObjects",
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
                                        "instanceId",
                                        new ToolOutputSchema { Type = "integer", Description = "GameObject instance ID" }
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
                                        "tag",
                                        new ToolOutputSchema { Type = "string", Description = "Tag" }
                                    },
                                    {
                                        "layer",
                                        new ToolOutputSchema { Type = "integer", Description = "Layer index" }
                                    }
                                }
                            }
                        }
                    }
                },
                Notes = "Read-only. Searches scene hierarchy only; no objects are selected or modified."
            };
        }
    }
}

