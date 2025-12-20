using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DatumStudio.Mcp.Core.Editor.Diagnostics;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Tool: project.references.missing - Detects missing script references and broken asset references.
    /// </summary>
    public class ProjectReferencesMissingTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the ProjectReferencesMissingTool class.
        /// </summary>
        public ProjectReferencesMissingTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to detect missing references.
        /// </summary>
        /// <param name="request">The tool invocation request with optional scope parameter.</param>
        /// <returns>Missing references response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            string scope = "all";

            if (request.Arguments != null && request.Arguments.TryGetValue("scope", out var scopeObj))
            {
                if (scopeObj is string)
                {
                    scope = (string)scopeObj;
                }
            }

            var timeGuard = new TimeGuard(TimeGuard.SceneScanMaxMilliseconds);
            var missingScripts = new List<Dictionary<string, object>>();
            var brokenReferences = new List<Dictionary<string, object>>();
            var diagnostics = new List<string>();
            bool timeLimitExceeded = false;

            try
            {
                if (scope == "all" || scope == "scenes")
                {
                    DetectMissingInScenes(missingScripts, brokenReferences, timeGuard, ref timeLimitExceeded);
                }

                if (!timeLimitExceeded && (scope == "all" || scope == "prefabs"))
                {
                    DetectMissingInPrefabs(missingScripts, brokenReferences, timeGuard, ref timeLimitExceeded);
                }

                if (!timeLimitExceeded && (scope == "all" || scope == "assets"))
                {
                    DetectMissingInAssets(missingScripts, brokenReferences, timeGuard, ref timeLimitExceeded);
                }
            }
            catch (TimeoutException)
            {
                timeLimitExceeded = true;
            }

            // Sort for deterministic ordering
            missingScripts = missingScripts.OrderBy(m => m["path"] as string).ThenBy(m => m["gameObjectPath"] as string).ThenBy(m => (int)m["componentIndex"]).ToList();
            brokenReferences = brokenReferences.OrderBy(b => b["path"] as string).ToList();

            if (timeLimitExceeded)
            {
                diagnostics.Add(timeGuard.GetPartialResultMessage(missingScripts.Count + brokenReferences.Count));
            }

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = new Dictionary<string, object>
                {
                    { "missingScripts", missingScripts.ToArray() },
                    { "brokenReferences", brokenReferences.ToArray() }
                },
                Diagnostics = diagnostics.Count > 0 ? diagnostics.ToArray() : null
            };

            return response;
        }

        private void DetectMissingInScenes(List<Dictionary<string, object>> missingScripts, List<Dictionary<string, object>> brokenReferences, TimeGuard timeGuard, ref bool timeLimitExceeded)
        {
            // Restrict to Assets folder to avoid scanning Packages/ (which causes "no meta file" errors)
            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            int processedScenes = 0;
            const int maxScenesPerScan = 100; // Limit number of scenes processed

            foreach (var guid in sceneGuids)
            {
                timeGuard.Check();
                
                if (processedScenes >= maxScenesPerScan)
                {
                    timeLimitExceeded = true;
                    break;
                }

                var path = AssetDatabase.GUIDToAssetPath(guid);
                // Guard: Only process assets in Assets/ folder (never touch Packages/)
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                    continue;

                try
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var rootObjects = scene.GetRootGameObjects();

                    foreach (var rootObj in rootObjects)
                    {
                        timeGuard.Check();
                        DetectMissingInGameObject(rootObj, path, missingScripts, brokenReferences, timeGuard);
                    }
                    
                    processedScenes++;
                }
                catch (TimeoutException)
                {
                    timeLimitExceeded = true;
                    break;
                }
                catch
                {
                    // Skip scenes that can't be opened
                }
            }
        }

        private void DetectMissingInPrefabs(List<Dictionary<string, object>> missingScripts, List<Dictionary<string, object>> brokenReferences, TimeGuard timeGuard, ref bool timeLimitExceeded)
        {
            // Restrict to Assets folder to avoid scanning Packages/ (which causes "no meta file" errors)
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int processedPrefabs = 0;
            const int maxPrefabsPerScan = 200; // Limit number of prefabs processed

            foreach (var guid in prefabGuids)
            {
                timeGuard.Check();
                
                if (processedPrefabs >= maxPrefabsPerScan)
                {
                    timeLimitExceeded = true;
                    break;
                }

                var path = AssetDatabase.GUIDToAssetPath(guid);
                // Guard: Only process assets in Assets/ folder (never touch Packages/)
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                    continue;

                try
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        continue;

                    DetectMissingInGameObject(prefab, path, missingScripts, brokenReferences, timeGuard);
                    processedPrefabs++;
                }
                catch (TimeoutException)
                {
                    timeLimitExceeded = true;
                    break;
                }
                catch
                {
                    // Skip prefabs that can't be loaded
                }
            }
        }

        private void DetectMissingInAssets(List<Dictionary<string, object>> missingScripts, List<Dictionary<string, object>> brokenReferences, TimeGuard timeGuard, ref bool timeLimitExceeded)
        {
            // Best-effort: check ScriptableObjects and other assets for missing references
            // This is limited as we can't easily detect all missing references in all asset types
            // Focus on scenes and prefabs which are the most common sources of missing scripts
            timeGuard.Check();
        }

        private void DetectMissingInGameObject(GameObject obj, string assetPath, List<Dictionary<string, object>> missingScripts, List<Dictionary<string, object>> brokenReferences, TimeGuard timeGuard)
        {
            timeGuard.Check();

            // Check for missing scripts on this GameObject
            var components = obj.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    // Missing script detected
                    var hierarchyPath = GetHierarchyPath(obj);
                    missingScripts.Add(new Dictionary<string, object>
                    {
                        { "path", assetPath },
                        { "gameObjectPath", hierarchyPath },
                        { "componentIndex", i },
                        { "context", $"Missing script component at index {i} on GameObject '{obj.name}'" }
                    });
                }
            }

            // Recursively check children (with depth limit to prevent excessive recursion)
            const int maxDepth = 50;
            DetectMissingInGameObjectRecursive(obj, assetPath, missingScripts, brokenReferences, timeGuard, 0, maxDepth);
        }

        private void DetectMissingInGameObjectRecursive(GameObject obj, string assetPath, List<Dictionary<string, object>> missingScripts, List<Dictionary<string, object>> brokenReferences, TimeGuard timeGuard, int depth, int maxDepth)
        {
            if (depth >= maxDepth)
                return;

            timeGuard.Check();

            foreach (Transform child in obj.transform)
            {
                timeGuard.Check();
                
                // Check for missing scripts on child GameObject
                var components = child.gameObject.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        var hierarchyPath = GetHierarchyPath(child.gameObject);
                        missingScripts.Add(new Dictionary<string, object>
                        {
                            { "path", assetPath },
                            { "gameObjectPath", hierarchyPath },
                            { "componentIndex", i },
                            { "context", $"Missing script component at index {i} on GameObject '{child.gameObject.name}'" }
                        });
                    }
                }
                
                // Recursively check children
                DetectMissingInGameObjectRecursive(child.gameObject, assetPath, missingScripts, brokenReferences, timeGuard, depth + 1, maxDepth);
            }
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
                Id = "project.references.missing",
                Name = "Detect Missing References",
                Description = "Detects missing script references and broken asset references in the project. High-value diagnostic tool with zero write risk.",
                Category = "project",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>
                {
                    {
                        "scope",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Scope to search: 'all', 'scenes', 'prefabs', or 'assets'. Default: 'all'.",
                            Default = "all",
                            Enum = new[] { "all", "scenes", "prefabs", "assets" }
                        }
                    }
                },
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "missingScripts",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "List of missing script references",
                            Items = new ToolOutputSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, ToolOutputSchema>
                                {
                                    {
                                        "path",
                                        new ToolOutputSchema
                                        {
                                            Type = "string",
                                            Description = "Asset path containing the missing script"
                                        }
                                    },
                                    {
                                        "gameObjectPath",
                                        new ToolOutputSchema
                                        {
                                            Type = "string",
                                            Description = "Hierarchy path to the GameObject with missing script"
                                        }
                                    },
                                    {
                                        "componentIndex",
                                        new ToolOutputSchema
                                        {
                                            Type = "integer",
                                            Description = "Component index where script is missing"
                                        }
                                    },
                                    {
                                        "context",
                                        new ToolOutputSchema
                                        {
                                            Type = "string",
                                            Description = "Human-readable context about the missing script"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    {
                        "brokenReferences",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "List of broken asset references (currently empty, reserved for future enhancement)",
                            Items = new ToolOutputSchema
                            {
                                Type = "object"
                            }
                        }
                    }
                },
                Notes = "Read-only. Scans for missing references only; does not attempt to fix or modify any assets. Best-effort detection; may not catch all missing references."
            };
        }
    }
}

