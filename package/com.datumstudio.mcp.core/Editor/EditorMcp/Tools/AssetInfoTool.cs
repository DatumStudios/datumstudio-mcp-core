using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Tool: asset.info - Returns detailed information about a specific asset.
    /// </summary>
    public class AssetInfoTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the AssetInfoTool class.
        /// </summary>
        public AssetInfoTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to return asset information.
        /// </summary>
        /// <param name="request">The tool invocation request with assetPath or guid.</param>
        /// <returns>Asset information response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            string assetPath = null;
            string guid = null;

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

            // If GUID was provided, verify it matches
            if (!string.IsNullOrEmpty(guid) && pathGuid != guid)
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", $"GUID '{guid}' does not match asset at path '{assetPath}'" }
                    }
                };
            }

            // Get asset information
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var mainObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var allDependencies = AssetDatabase.GetDependencies(assetPath, false);
            
            // Filter to only Assets/ dependencies (never touch Packages/)
            int dependencyCount = 0;
            if (allDependencies != null)
            {
                foreach (var dep in allDependencies)
                {
                    if (!string.IsNullOrEmpty(dep) && dep.StartsWith("Assets/") && !dep.EndsWith(".meta"))
                    {
                        dependencyCount++;
                    }
                }
            }
            
            var importer = AssetImporter.GetAtPath(assetPath);

            var output = new Dictionary<string, object>
            {
                { "path", assetPath },
                { "guid", assetGuid },
                { "type", assetType != null ? assetType.Name : "Unknown" },
                { "mainObjectName", mainObject != null ? mainObject.name : "" },
                { "dependencyCount", dependencyCount }
            };

            if (importer != null)
            {
                output["importerType"] = importer.GetType().Name;
            }
            else
            {
                output["importerType"] = null;
            }

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = output
            };

            return response;
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "asset.info",
                Name = "Asset Info",
                Description = "Returns detailed information about a specific asset including type, dependencies, and import settings. Demonstrates asset graph awareness without mutation.",
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
                            Description = "Path to the asset (e.g., 'Assets/Textures/Logo.png'). Either assetPath or guid must be provided."
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
                    }
                },
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "path",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Asset path"
                        }
                    },
                    {
                        "guid",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Asset GUID"
                        }
                    },
                    {
                        "type",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Main asset type name"
                        }
                    },
                    {
                        "mainObjectName",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Name of the main asset object"
                        }
                    },
                    {
                        "dependencyCount",
                        new ToolOutputSchema
                        {
                            Type = "integer",
                            Description = "Number of direct dependencies"
                        }
                    },
                    {
                        "importerType",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Asset importer type name (null if no importer)"
                        }
                    }
                },
                Notes = "Read-only. Reads asset metadata and import settings only; no asset files or import settings are modified."
            };
        }
    }
}

