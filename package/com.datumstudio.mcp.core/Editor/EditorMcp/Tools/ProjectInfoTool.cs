using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Tool: project.info - Returns high-level project information.
    /// </summary>
    public class ProjectInfoTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the ProjectInfoTool class.
        /// </summary>
        public ProjectInfoTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to return project information.
        /// </summary>
        /// <param name="request">The tool invocation request (no arguments required).</param>
        /// <returns>Project information response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            var productName = PlayerSettings.productName;
            var unityVersion = Application.unityVersion;
            var platform = Application.platform.ToString();
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            var renderPipeline = DetectRenderPipeline();

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = new Dictionary<string, object>
                {
                    { "productName", productName },
                    { "unityVersion", unityVersion },
                    { "platform", platform },
                    { "activeBuildTarget", activeBuildTarget },
                    { "renderPipeline", renderPipeline }
                }
            };

            return response;
        }

        private string DetectRenderPipeline()
        {
            var renderPipelineAsset = GraphicsSettings.renderPipelineAsset;
            if (renderPipelineAsset == null)
            {
                return "Built-in";
            }

            var assetType = renderPipelineAsset.GetType().Name;
            if (assetType.Contains("Universal") || assetType.Contains("URP"))
            {
                return "URP";
            }
            if (assetType.Contains("HighDefinition") || assetType.Contains("HDRP"))
            {
                return "HDRP";
            }

            return "Unknown";
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "project.info",
                Name = "Project Info",
                Description = "Returns high-level project information including Unity version, render pipeline, build targets, and project configuration. Provides foundational context for all other operations.",
                Category = "project",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>(),
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "productName",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Project product name"
                        }
                    },
                    {
                        "unityVersion",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Unity Editor version"
                        }
                    },
                    {
                        "platform",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Current platform"
                        }
                    },
                    {
                        "activeBuildTarget",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Active build target (e.g., StandaloneWindows64)"
                        }
                    },
                    {
                        "renderPipeline",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Render pipeline: URP, HDRP, Built-in, or Unknown"
                        }
                    }
                },
                Notes = "Read-only. Reads project settings only; no modifications are made."
            };
        }
    }
}

