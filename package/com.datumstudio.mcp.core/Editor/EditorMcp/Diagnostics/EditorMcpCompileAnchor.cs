using UnityEngine;

namespace DatumStudio.Mcp.Core.Editor.Diagnostics
{
    /// <summary>
    /// Exists solely to guarantee this package compiles an Editor assembly in all projects.
    /// Do not remove. Used by harness diagnostics to verify package compilation.
    /// This type has minimal dependencies (only UnityEngine.Debug) to ensure it always compiles.
    /// </summary>
    internal static class EditorMcpCompileAnchor
    {
        /// <summary>
        /// Package identifier constant.
        /// </summary>
        public const string PackageId = "com.datumstudio.mcp.core";

        /// <summary>
        /// Package version constant.
        /// </summary>
        public const string Version = "0.1.1";

        /// <summary>
        /// Assembly name that should be produced by this package.
        /// </summary>
        public const string AssemblyName = "DatumStudio.Mcp.Core.Editor";

        /// <summary>
        /// Logs a diagnostic message (only if verbose logging is enabled).
        /// This method ensures the type is referenced and compiled.
        /// </summary>
        internal static void LogDiagnostic(string message)
        {
            // Only log if verbose mode is enabled (can be controlled via preprocessor directives)
            #if UNITY_EDITOR && VERBOSE_EDITOR_MCP
            Debug.Log($"[EditorMCP] {message}");
            #endif
        }
    }
}

