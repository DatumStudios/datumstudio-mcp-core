using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Tool: project.scenes.list - Lists all scenes in the project.
    /// </summary>
    public class ProjectScenesListTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the ProjectScenesListTool class.
        /// </summary>
        public ProjectScenesListTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to list scenes.
        /// </summary>
        /// <param name="request">The tool invocation request with optional includeAllScenes flag.</param>
        /// <returns>List of scenes response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            bool includeAllScenes = false;

            if (request.Arguments != null && request.Arguments.TryGetValue("includeAllScenes", out var includeAllObj))
            {
                if (includeAllObj is bool)
                {
                    includeAllScenes = (bool)includeAllObj;
                }
            }

            var scenes = new List<Dictionary<string, object>>();

            // Always include Build Settings scenes
            var buildScenes = EditorBuildSettings.scenes;
            var buildScenePaths = new HashSet<string>();

            foreach (var buildScene in buildScenes)
            {
                if (string.IsNullOrEmpty(buildScene.path))
                    continue;

                buildScenePaths.Add(buildScene.path);
                scenes.Add(new Dictionary<string, object>
                {
                    { "path", buildScene.path },
                    { "name", System.IO.Path.GetFileNameWithoutExtension(buildScene.path) },
                    { "enabledInBuild", buildScene.enabled },
                    { "buildIndex", Array.IndexOf(buildScenes, buildScene) }
                });
            }

            // Optionally include all scenes in Assets (restrict to Assets folder to avoid scanning Packages/)
            if (includeAllScenes)
            {
                var allSceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
                foreach (var guid in allSceneGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    // Guard: Only process assets in Assets/ folder (never touch Packages/)
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                        continue;

                    // Skip if already in build settings
                    if (buildScenePaths.Contains(path))
                        continue;

                    scenes.Add(new Dictionary<string, object>
                    {
                        { "path", path },
                        { "name", System.IO.Path.GetFileNameWithoutExtension(path) },
                        { "enabledInBuild", false },
                        { "buildIndex", -1 }
                    });
                }
            }

            // Sort by path for stable ordering
            scenes = scenes.OrderBy(s => s["path"] as string).ToList();

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = new Dictionary<string, object>
                {
                    { "scenes", scenes.ToArray() }
                }
            };

            return response;
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "project.scenes.list",
                Name = "List Project Scenes",
                Description = "Lists all scenes in the project with their paths and build settings status. Provides foundational context for scene-based operations.",
                Category = "project",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>
                {
                    {
                        "includeAllScenes",
                        new ToolParameterSchema
                        {
                            Type = "boolean",
                            Required = false,
                            Description = "If true, includes all scenes in Assets folder, not just Build Settings. Default: false (Build Settings only).",
                            Default = false
                        }
                    }
                },
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "scenes",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "List of scenes",
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
                                            Description = "Scene file path (e.g., 'Assets/Scenes/Main.unity')"
                                        }
                                    },
                                    {
                                        "name",
                                        new ToolOutputSchema
                                        {
                                            Type = "string",
                                            Description = "Scene name (filename without extension)"
                                        }
                                    },
                                    {
                                        "enabledInBuild",
                                        new ToolOutputSchema
                                        {
                                            Type = "boolean",
                                            Description = "Whether scene is enabled in Build Settings"
                                        }
                                    },
                                    {
                                        "buildIndex",
                                        new ToolOutputSchema
                                        {
                                            Type = "integer",
                                            Description = "Build index (-1 if not in build settings)"
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                Notes = "Read-only. Reads scene list from EditorBuildSettings; no scene files are modified."
            };
        }
    }
}

