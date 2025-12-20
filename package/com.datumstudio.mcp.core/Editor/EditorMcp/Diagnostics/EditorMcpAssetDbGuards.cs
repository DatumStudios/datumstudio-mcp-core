using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DatumStudio.Mcp.Core.Editor.Diagnostics
{
    /// <summary>
    /// Centralized guards for AssetDatabase and file I/O operations to prevent touching Packages/ paths.
    /// All AssetDatabase calls must only operate on Assets/ paths to avoid "immutable folder / no meta file" errors.
    /// All file I/O must avoid PackageCache/ to prevent "immutable package altered" errors.
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
        /// Checks if a file system path is in PackageCache or Packages folder (immutable, must not write).
        /// </summary>
        /// <param name="filePath">Full file system path to check.</param>
        /// <returns>True if path is in PackageCache/ or Packages/, false otherwise.</returns>
        public static bool IsPackageCachePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Normalize path separators for cross-platform compatibility
            var normalizedPath = filePath.Replace('\\', '/');
            
            // Check for PackageCache in path (case-insensitive on Windows)
            if (normalizedPath.IndexOf("/PackageCache/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            
            // Check for Packages/ in path (Unity asset path format)
            if (normalizedPath.IndexOf("/Packages/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            
            return false;
        }

        /// <summary>
        /// Validates that a file path is safe for writing (not in PackageCache or Packages).
        /// Throws ArgumentException if path is in an immutable package location.
        /// </summary>
        /// <param name="filePath">Full file system path to validate.</param>
        /// <param name="operation">Name of the operation (for error message).</param>
        /// <exception cref="ArgumentException">Thrown when path is in PackageCache or Packages.</exception>
        public static void ValidateWritePath(string filePath, string operation = "file write")
        {
            if (IsPackageCachePath(filePath))
            {
                throw new ArgumentException(
                    $"Cannot {operation} to immutable package location: {filePath}. " +
                    "PackageCache and Packages/ folders are read-only. " +
                    "Use Application.dataPath (Assets/) or 'Library/' for temporary files.",
                    nameof(filePath));
            }
        }

        /// <summary>
        /// Gets a safe path for temporary files in the Unity project Library folder.
        /// This is the recommended location for package-generated temporary files.
        /// </summary>
        /// <param name="relativePath">Relative path within Library/ (e.g., "EditorMCP/logs.txt").</param>
        /// <returns>Full path to Library/relativePath, with directory created if needed.</returns>
        public static string GetLibraryPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                throw new ArgumentException("Relative path cannot be null or empty.", nameof(relativePath));

            // Remove leading slashes and normalize
            relativePath = relativePath.TrimStart('/', '\\').Replace('\\', '/');
            
            // Ensure it doesn't try to escape Library/
            if (relativePath.Contains("../") || relativePath.StartsWith("../"))
                throw new ArgumentException("Relative path cannot contain '..' to escape Library/", nameof(relativePath));

            var libraryPath = Path.Combine(Application.dataPath, "..", "Library", relativePath);
            var directory = Path.GetDirectoryName(libraryPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return libraryPath;
        }

        /// <summary>
        /// Gets a safe path for files in the Unity project Assets folder.
        /// </summary>
        /// <param name="relativePath">Relative path within Assets/ (e.g., "EditorMCP/config.json").</param>
        /// <returns>Full path to Assets/relativePath, with directory created if needed.</returns>
        public static string GetAssetsPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                throw new ArgumentException("Relative path cannot be null or empty.", nameof(relativePath));

            // Remove leading slashes and normalize
            relativePath = relativePath.TrimStart('/', '\\').Replace('\\', '/');
            
            // Ensure it doesn't try to escape Assets/
            if (relativePath.Contains("../") || relativePath.StartsWith("../"))
                throw new ArgumentException("Relative path cannot contain '..' to escape Assets/", nameof(relativePath));

            var assetsPath = Path.Combine(Application.dataPath, relativePath);
            var directory = Path.GetDirectoryName(assetsPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Final safety check
            ValidateWritePath(assetsPath, "write to Assets");

            return assetsPath;
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

