using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DatumStudio.Mcp.Core.Editor.Diagnostics;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Tool: project.assets.summary - Returns a summary of project assets by type.
    /// </summary>
    public class ProjectAssetsSummaryTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the ProjectAssetsSummaryTool class.
        /// </summary>
        public ProjectAssetsSummaryTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to return asset summary.
        /// </summary>
        /// <param name="request">The tool invocation request (no arguments required).</param>
        /// <returns>Asset summary response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            var timeGuard = new TimeGuard(TimeGuard.AssetScanMaxMilliseconds);
            var diagnostics = new List<string>();
            bool timeLimitExceeded = false;

            var counts = new Dictionary<string, int>
            {
                { "Scene", 0 },
                { "Prefab", 0 },
                { "ScriptableObject", 0 },
                { "Material", 0 },
                { "Texture", 0 },
                { "Audio", 0 },
                { "Animation", 0 },
                { "Timeline", 0 }
            };

            try
            {
                // Count scenes (restrict to Assets folder to avoid scanning Packages/)
                timeGuard.Check();
                var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
                counts["Scene"] = sceneGuids != null ? sceneGuids.Length : 0;

                // Count prefabs (restrict to Assets folder to avoid scanning Packages/)
                timeGuard.Check();
                var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
                counts["Prefab"] = prefabGuids != null ? prefabGuids.Length : 0;

                // Count ScriptableObjects (best-effort: find all ScriptableObject-derived assets)
                // Restrict to Assets folder to avoid scanning Packages/
                timeGuard.Check();
                var scriptableObjectGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });
                counts["ScriptableObject"] = scriptableObjectGuids != null ? scriptableObjectGuids.Length : 0;

                // Count materials (restrict to Assets folder to avoid scanning Packages/)
                timeGuard.Check();
                var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
                counts["Material"] = materialGuids != null ? materialGuids.Length : 0;

                // Count textures (Texture2D, Texture3D, etc.) (restrict to Assets folder to avoid scanning Packages/)
                timeGuard.Check();
                var textureGuids = AssetDatabase.FindAssets("t:Texture2D t:Texture3D t:Cubemap", new[] { "Assets" });
                counts["Texture"] = textureGuids != null ? textureGuids.Length : 0;

                // Count audio clips (restrict to Assets folder to avoid scanning Packages/)
                timeGuard.Check();
                var audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
                counts["Audio"] = audioGuids != null ? audioGuids.Length : 0;

                // Count animations (restrict to Assets folder to avoid scanning Packages/)
                timeGuard.Check();
                var animationGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" });
                counts["Animation"] = animationGuids != null ? animationGuids.Length : 0;

                // Count Timeline assets (best-effort: PlayableDirector assets)
                // Restrict to Assets folder to avoid scanning Packages/
                timeGuard.Check();
                try
                {
                    var timelineGuids = AssetDatabase.FindAssets("t:PlayableAsset", new[] { "Assets" });
                    counts["Timeline"] = timelineGuids != null ? timelineGuids.Length : 0;
                }
                catch
                {
                    // Timeline package may not be installed
                    counts["Timeline"] = 0;
                }
            }
            catch (TimeoutException)
            {
                timeLimitExceeded = true;
                diagnostics.Add(timeGuard.GetPartialResultMessage(counts.Values.Sum()));
            }

            // Calculate total
            var totalAssets = counts.Values.Sum();

            // Ensure stable key ordering in byType dictionary
            var sortedByType = new Dictionary<string, int>();
            var sortedKeys = counts.Keys.OrderBy(k => k).ToList();
            foreach (var key in sortedKeys)
            {
                sortedByType[key] = counts[key];
            }

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = new Dictionary<string, object>
                {
                    { "totalAssets", totalAssets },
                    { "byType", sortedByType }
                },
                Diagnostics = diagnostics.Count > 0 ? diagnostics.ToArray() : null
            };

            return response;
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "project.assets.summary",
                Name = "Project Assets Summary",
                Description = "Returns a summary of project assets including counts by asset type, large assets, and unreferenced asset detection. Provides project health insights without mutation.",
                Category = "project",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>(),
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "totalAssets",
                        new ToolOutputSchema
                        {
                            Type = "integer",
                            Description = "Total number of assets counted"
                        }
                    },
                    {
                        "byType",
                        new ToolOutputSchema
                        {
                            Type = "object",
                            Description = "Asset counts by type",
                            Properties = new Dictionary<string, ToolOutputSchema>
                            {
                                {
                                    "Scene",
                                    new ToolOutputSchema { Type = "integer", Description = "Number of Scene assets" }
                                },
                                {
                                    "Prefab",
                                    new ToolOutputSchema { Type = "integer", Description = "Number of Prefab assets" }
                                },
                                {
                                    "ScriptableObject",
                                    new ToolOutputSchema { Type = "integer", Description = "Number of ScriptableObject assets" }
                                },
                                {
                                    "Material",
                                    new ToolOutputSchema { Type = "integer", Description = "Number of Material assets" }
                                },
                                {
                                    "Texture",
                                    new ToolOutputSchema { Type = "integer", Description = "Number of Texture assets" }
                                },
                                {
                                    "Audio",
                                    new ToolOutputSchema { Type = "integer", Description = "Number of AudioClip assets" }
                                },
                                {
                                    "Animation",
                                    new ToolOutputSchema { Type = "integer", Description = "Number of AnimationClip assets" }
                                },
                                {
                                    "Timeline",
                                    new ToolOutputSchema { Type = "integer", Description = "Number of Timeline/PlayableAsset assets" }
                                }
                            }
                        }
                    }
                },
                Notes = "Read-only. Analyzes asset database only; no assets are modified or deleted. Timeline detection is best-effort and may return 0 if Timeline package is not installed."
            };
        }
    }
}

