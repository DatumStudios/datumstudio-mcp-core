using DatumStudio.Mcp.Core.Editor.Registry;
using DatumStudio.Mcp.Core.Editor.Tools;

namespace DatumStudio.Mcp.Core.Editor.Server
{
    /// <summary>
    /// Bootstraps core MCP platform tools for EditorMCP v0.1.
    /// Registers the foundational tools required for MCP protocol compliance.
    /// </summary>
    public static class CoreToolBootstrapper
    {
        /// <summary>
        /// Registers all core MCP platform tools with the tool registry.
        /// </summary>
        /// <param name="toolRegistry">The tool registry to register tools with.</param>
        /// <param name="serverVersion">The EditorMCP server version (default: "0.1.0").</param>
        public static void RegisterCoreTools(ToolRegistry toolRegistry, string serverVersion = "0.1.0")
        {
            if (toolRegistry == null)
                return;

            // Register MCP platform tools
            var serverInfoTool = new McpServerInfoTool(toolRegistry, serverVersion);
            toolRegistry.Register(serverInfoTool);

            var toolsListTool = new McpToolsListTool(toolRegistry);
            toolRegistry.Register(toolsListTool);

            var toolDescribeTool = new McpToolDescribeTool(toolRegistry);
            toolRegistry.Register(toolDescribeTool);

            // Register project inspection tools
            var projectInfoTool = new ProjectInfoTool();
            toolRegistry.Register(projectInfoTool);

            var projectScenesListTool = new ProjectScenesListTool();
            toolRegistry.Register(projectScenesListTool);

            var projectAssetsSummaryTool = new ProjectAssetsSummaryTool();
            toolRegistry.Register(projectAssetsSummaryTool);

            var projectReferencesMissingTool = new ProjectReferencesMissingTool();
            toolRegistry.Register(projectReferencesMissingTool);

            // Register scene inspection tools
            var sceneHierarchyDumpTool = new SceneHierarchyDumpTool();
            toolRegistry.Register(sceneHierarchyDumpTool);

            var sceneObjectsFindTool = new SceneObjectsFindTool();
            toolRegistry.Register(sceneObjectsFindTool);

            var sceneComponentsListTool = new SceneComponentsListTool();
            toolRegistry.Register(sceneComponentsListTool);

            // Register asset inspection tools
            var assetInfoTool = new AssetInfoTool();
            toolRegistry.Register(assetInfoTool);

            var assetDependenciesGraphTool = new AssetDependenciesGraphTool();
            toolRegistry.Register(assetDependenciesGraphTool);

            // Register audio tools
            var audioMixerListTool = new AudioMixerListTool();
            toolRegistry.Register(audioMixerListTool);

            var audioMixerSnapshotReadTool = new AudioMixerSnapshotReadTool();
            toolRegistry.Register(audioMixerSnapshotReadTool);

            // Register editor state tool
            var editorSelectionInfoTool = new EditorSelectionInfoTool();
            toolRegistry.Register(editorSelectionInfoTool);
        }
    }
}

