using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DatumStudios.EditorMCP.Schemas;

namespace DatumStudios.EditorMCP.Tools
{
    /// <summary>
    /// Tool: asset.dependencies.graph - Returns the dependency graph for an asset.
    /// </summary>
    public class AssetDependenciesGraphTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the AssetDependenciesGraphTool class.
        /// </summary>
        public AssetDependenciesGraphTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to return dependency graph.
        /// </summary>
        /// <param name="request">The tool invocation request with assetPath or guid and optional depth.</param>
        /// <returns>Dependency graph response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            string assetPath = null;
            string guid = null;
            int depth = 1;
            string direction = "both";

            if (request.Arguments != null)
            {
                if (request.Arguments.TryGetValue("assetPath", out var assetPathObj) && assetPathObj is string)
                {
                    assetPath = (string)assetPathObj;
                }

                if (request.Arguments.TryGetValue("guid", out var guidObj) && guidObj is string)
                {
                    guid = (string)guidObj;
                }

                if (request.Arguments.TryGetValue("depth", out var depthObj))
                {
                    if (depthObj is int)
                    {
                        depth = (int)depthObj;
                    }
                    else if (depthObj is long)
                    {
                        depth = (int)(long)depthObj;
                    }
                }

                if (request.Arguments.TryGetValue("direction", out var directionObj) && directionObj is string)
                {
                    direction = (string)directionObj;
                }
            }

            // Validate input
            if (string.IsNullOrEmpty(assetPath) && string.IsNullOrEmpty(guid))
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", "Either 'assetPath' or 'guid' must be provided" }
                    }
                };
            }

            // Validate depth
            if (depth < 1)
            {
                depth = 1;
            }
            if (depth > 10)
            {
                depth = 10; // Cap at reasonable depth
            }

            // Convert GUID to path if needed
            if (!string.IsNullOrEmpty(guid))
            {
                assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    return new ToolInvokeResponse
                    {
                        Tool = Definition.Id,
                        Output = new Dictionary<string, object>
                        {
                            { "error", $"Asset with GUID '{guid}' not found" }
                        }
                    };
                }
            }

            // Guard: Only process assets in Assets/ folder (never touch Packages/)
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/"))
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", $"Asset path must be in Assets/ folder. Package assets are not supported." }
                    }
                };
            }

            // Validate path exists
            var pathGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(pathGuid))
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", $"Asset at path '{assetPath}' not found" }
                    }
                };
            }

            // Build dependency graph
            var dependencies = new List<Dictionary<string, object>>();
            var dependents = new List<Dictionary<string, object>>();

            if (direction == "dependencies" || direction == "both")
            {
                dependencies = BuildDependencyList(assetPath, depth, true);
            }

            if (direction == "dependents" || direction == "both")
            {
                dependents = BuildDependentList(assetPath, depth);
            }

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = new Dictionary<string, object>
                {
                    { "assetPath", assetPath },
                    { "dependencies", dependencies.ToArray() },
                    { "dependents", dependents.ToArray() }
                }
            };

            return response;
        }

        private List<Dictionary<string, object>> BuildDependencyList(string assetPath, int maxDepth, bool recursive)
        {
            var result = new List<Dictionary<string, object>>();
            var visited = new HashSet<string>();
            var queue = new Queue<(string path, int currentDepth)>();

            queue.Enqueue((assetPath, 0));

            while (queue.Count > 0)
            {
                var (currentPath, currentDepth) = queue.Dequeue();

                if (currentDepth >= maxDepth || visited.Contains(currentPath))
                    continue;

                visited.Add(currentPath);

                var deps = AssetDatabase.GetDependencies(currentPath, recursive);
                if (deps != null)
                {
                    // Sort for stable ordering
                    var sortedDeps = deps.OrderBy(d => d).ToList();

                    foreach (var dep in sortedDeps)
                    {
                        // Skip self, meta files, and package paths (never touch Packages/)
                        if (dep == currentPath || dep.EndsWith(".meta") || !dep.StartsWith("Assets/"))
                            continue;

                        var depType = AssetDatabase.GetMainAssetTypeAtPath(dep);
                        result.Add(new Dictionary<string, object>
                        {
                            { "path", dep },
                            { "guid", AssetDatabase.AssetPathToGUID(dep) },
                            { "type", depType != null ? depType.Name : "Unknown" },
                            { "depth", currentDepth + 1 }
                        });

                        if (currentDepth + 1 < maxDepth)
                        {
                            queue.Enqueue((dep, currentDepth + 1));
                        }
                    }
                }
            }

            // Sort by depth, then by path for stable ordering
            return result.OrderBy(d => (int)d["depth"]).ThenBy(d => (string)d["path"]).ToList();
        }

        private List<Dictionary<string, object>> BuildDependentList(string assetPath, int maxDepth)
        {
            var result = new List<Dictionary<string, object>>();
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var visited = new HashSet<string>();
            var queue = new Queue<(string path, int currentDepth)>();

            // Find all assets that depend on this one
            var allAssets = AssetDatabase.FindAssets("", new[] { "Assets" });
            var dependents = new List<string>();

            foreach (var guid in allAssets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                // Skip meta files and package paths (never touch Packages/)
                if (string.IsNullOrEmpty(path) || path.EndsWith(".meta") || !path.StartsWith("Assets/"))
                    continue;

                var deps = AssetDatabase.GetDependencies(path, false);
                if (deps != null && System.Array.IndexOf(deps, assetPath) >= 0)
                {
                    dependents.Add(path);
                }
            }

            // Build graph recursively
            queue.Enqueue((assetPath, 0));
            visited.Add(assetPath);

            foreach (var dependent in dependents.OrderBy(p => p))
            {
                // Guard: Skip package paths (never touch Packages/)
                if (!dependent.StartsWith("Assets/"))
                    continue;

                if (!visited.Contains(dependent))
                {
                    var depType = AssetDatabase.GetMainAssetTypeAtPath(dependent);
                    result.Add(new Dictionary<string, object>
                    {
                        { "path", dependent },
                        { "guid", AssetDatabase.AssetPathToGUID(dependent) },
                        { "type", depType != null ? depType.Name : "Unknown" },
                        { "depth", 1 }
                    });
                    visited.Add(dependent);

                    // For depth > 1, find dependents of dependents
                    if (maxDepth > 1)
                    {
                        var deeperDeps = BuildDependentList(dependent, maxDepth - 1);
                        foreach (var deeperDep in deeperDeps)
                        {
                            var deeperPath = (string)deeperDep["path"];
                            if (!visited.Contains(deeperPath))
                            {
                                deeperDep["depth"] = (int)deeperDep["depth"] + 1;
                                result.Add(deeperDep);
                                visited.Add(deeperPath);
                            }
                        }
                    }
                }
            }

            // Sort by depth, then by path for stable ordering
            return result.OrderBy(d => (int)d["depth"]).ThenBy(d => (string)d["path"]).ToList();
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "asset.dependencies.graph",
                Name = "Asset Dependencies Graph",
                Description = "Returns the dependency graph for an asset (what it depends on and what depends on it). Useful for understanding asset relationships and impact analysis.",
                Category = "asset",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>
                {
                    {
                        "assetPath",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Path to the asset. Either assetPath or guid must be provided."
                        }
                    },
                    {
                        "guid",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "GUID of the asset. Either assetPath or guid must be provided."
                        }
                    },
                    {
                        "depth",
                        new ToolParameterSchema
                        {
                            Type = "integer",
                            Required = false,
                            Description = "Maximum depth to traverse (default: 1, max: 10)",
                            Default = 1,
                            Minimum = 1,
                            Maximum = 10
                        }
                    },
                    {
                        "direction",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Direction: 'dependencies' (what this asset depends on), 'dependents' (what depends on this), or 'both' (default: 'both')",
                            Default = "both",
                            Enum = new[] { "dependencies", "dependents", "both" }
                        }
                    }
                },
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "assetPath",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Path to the asset"
                        }
                    },
                    {
                        "dependencies",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "List of assets this asset depends on",
                            Items = new ToolOutputSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, ToolOutputSchema>
                                {
                                    {
                                        "path",
                                        new ToolOutputSchema { Type = "string", Description = "Dependency asset path" }
                                    },
                                    {
                                        "guid",
                                        new ToolOutputSchema { Type = "string", Description = "Dependency asset GUID" }
                                    },
                                    {
                                        "type",
                                        new ToolOutputSchema { Type = "string", Description = "Dependency asset type" }
                                    },
                                    {
                                        "depth",
                                        new ToolOutputSchema { Type = "integer", Description = "Depth in dependency tree" }
                                    }
                                }
                            }
                        }
                    },
                    {
                        "dependents",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "List of assets that depend on this asset",
                            Items = new ToolOutputSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, ToolOutputSchema>
                                {
                                    {
                                        "path",
                                        new ToolOutputSchema { Type = "string", Description = "Dependent asset path" }
                                    },
                                    {
                                        "guid",
                                        new ToolOutputSchema { Type = "string", Description = "Dependent asset GUID" }
                                    },
                                    {
                                        "type",
                                        new ToolOutputSchema { Type = "string", Description = "Dependent asset type" }
                                    },
                                    {
                                        "depth",
                                        new ToolOutputSchema { Type = "integer", Description = "Depth in dependency tree" }
                                    }
                                }
                            }
                        }
                    }
                },
                Notes = "Read-only. Analyzes asset dependency graph only; no assets are modified or moved."
            };
        }
    }
}

