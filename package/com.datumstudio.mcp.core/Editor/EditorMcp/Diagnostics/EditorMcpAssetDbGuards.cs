using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace DatumStudio.Mcp.Core.Editor.Diagnostics
{
    /// <summary>
    /// Centralized guards for AssetDatabase operations to prevent touching Packages/ paths.
    /// All AssetDatabase calls must only operate on Assets/ paths to avoid "immutable folder / no meta file" errors.
    /// </summary>
    internal static class EditorMcpAssetDbGuards
    {
        /// <summary>
        /// Checks if a path is in the Assets/ folder (not Packages/).
        /// </summary>
        /// <param name="path">Asset path to check.</param>
        /// <returns>True if path is in Assets/, false otherwise.</returns>
        public static bool IsAssetsPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/");
        }

        /// <summary>
        /// Finds assets using AssetDatabase.FindAssets, restricted to Assets/ folder only.
        /// </summary>
        /// <param name="filter">Search filter (e.g., "t:Scene", "t:Prefab").</param>
        /// <returns>Array of GUIDs for assets in Assets/ folder only.</returns>
        public static string[] FindAssetsInAssetsOnly(string filter)
        {
            return AssetDatabase.FindAssets(filter, new[] { "Assets" });
        }

        /// <summary>
        /// Converts a GUID to an asset path, but only if the path is in Assets/.
        /// </summary>
        /// <param name="guid">Asset GUID.</param>
        /// <param name="assetsPath">Output asset path if valid, null otherwise.</param>
        /// <returns>True if GUID maps to an Assets/ path, false otherwise.</returns>
        public static bool TryGuidToAssetsPath(string guid, out string assetsPath)
        {
            assetsPath = null;
            
            if (string.IsNullOrEmpty(guid))
                return false;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            
            if (!IsAssetsPath(path))
                return false;

            assetsPath = path;
            return true;
        }

        /// <summary>
        /// Filters an array of paths to only include Assets/ paths.
        /// </summary>
        /// <param name="paths">Array of asset paths.</param>
        /// <returns>Enumerable of paths that are in Assets/ folder.</returns>
        public static IEnumerable<string> FilterToAssetsOnly(string[] paths)
        {
            if (paths == null)
                return Enumerable.Empty<string>();

            return paths.Where(IsAssetsPath);
        }

        /// <summary>
        /// Filters an enumerable of paths to only include Assets/ paths.
        /// </summary>
        /// <param name="paths">Enumerable of asset paths.</param>
        /// <returns>Enumerable of paths that are in Assets/ folder.</returns>
        public static IEnumerable<string> FilterToAssetsOnly(IEnumerable<string> paths)
        {
            if (paths == null)
                return Enumerable.Empty<string>();

            return paths.Where(IsAssetsPath);
        }
    }
}

