using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DatumStudios.EditorMCP.Helpers;
using DatumStudios.EditorMCP.Registry;

namespace DatumStudios.EditorMCP.Tools
{
    /// <summary>
    /// GameObject Operations Tools - Core tier tools for GameObject discovery and safe hierarchy management.
    /// All tools work on the active scene only (no scenePath parameter).
    /// </summary>
    [McpToolCategory("go")]
    public static class GameObjectTools
    {
        /// <summary>
        /// Finds GameObjects in the active scene matching specified criteria (hierarchy path pattern, component type, tag, layer).
        /// Essential discovery tool that enables 90% of Cursor queries starting with "find objects".
        /// </summary>
        /// <param name="jsonParams">JSON parameters with optional "hierarchyPath", "componentType", "tag", "layer", "namePattern", "maxResults".</param>
        /// <returns>JSON string with matching GameObjects.</returns>
        [McpTool("go.find", "Finds GameObjects in the active scene matching specified criteria (hierarchy path pattern, component type, tag, layer). Essential discovery tool that enables 90% of Cursor queries starting with \"find objects\".", Tier.Core)]
        public static Dictionary<string, object> FindGameObjects(string jsonParams)
        {
            // Check for active scene
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", "No active scene loaded" }
                };
            }

            // Parse JSON parameters
            string hierarchyPath = null;
            string componentType = null;
            string tag = null;
            int? layer = null;
            string namePattern = null;
            int maxResults = 100;

            if (!string.IsNullOrEmpty(jsonParams) && jsonParams != "{}")
            {
                var paramsObj = UnityEngine.JsonUtility.FromJson<Dictionary<string, object>>(jsonParams);
                if (paramsObj != null)
                {
                    if (paramsObj.TryGetValue("hierarchyPath", out var hierarchyPathObj) && hierarchyPathObj is string)
                    {
                        hierarchyPath = (string)hierarchyPathObj;
                    }

                    if (paramsObj.TryGetValue("componentType", out var componentTypeParam) && componentTypeParam is string)
                    {
                        componentType = (string)componentTypeParam;
                    }

                    if (paramsObj.TryGetValue("tag", out var tagObj) && tagObj is string)
                    {
                        tag = (string)tagObj;
                    }

                    if (paramsObj.TryGetValue("layer", out var layerObj))
                    {
                        if (layerObj is int)
                        {
                            layer = (int)layerObj;
                        }
                        else if (layerObj is long)
                        {
                            layer = (int)(long)layerObj;
                        }
                    }
                    }
                }
}

            // Filter GameObjects
            foreach (var obj in allObjects)
            {
                if (matches.Count >= maxResults)
                    break;

                // Check hierarchy path pattern
                if (hierarchyRegex != null)
                {
                    var fullPath = HierarchyResolver.FullHierarchyPath(obj.transform);
                    if (!hierarchyRegex.IsMatch(fullPath))
                        continue;
                }

                // Check name pattern
                if (!string.IsNullOrEmpty(namePattern))
                {
                    if (!MatchesNamePattern(obj.name, namePattern))
                        continue;
                }

                // Check tag
                if (!string.IsNullOrEmpty(tag))
                {
                    if (!obj.CompareTag(tag))
                        continue;
                }

                // Check layer
                if (layer.HasValue)
                {
                    if (obj.layer != layer.Value)
                        continue;
                }

                // Check component type
                if (componentTypeObj != null)
                {
                    if (obj.GetComponent(componentTypeObj) == null)
                        continue;
                }

                // Collect component type names
                var componentNames = new List<string>();
                foreach (var comp in obj.GetComponents<Component>())
                {
                    if (comp != null)
                    {
                        componentNames.Add(comp.GetType().FullName);
                    }
                }

                matches.Add(new Dictionary<string, object>
                {
                    { "instanceId", obj.GetInstanceID() },
                    { "name", obj.name },
                    { "hierarchyPath", HierarchyResolver.FullHierarchyPath(obj.transform) },
                    { "components", componentNames.ToArray() },
                    { "tag", obj.tag },
                    { "layer", obj.layer }
                };
            }

            // Sort by hierarchy path for deterministic output
            matches = matches.OrderBy(m => (string)m["hierarchyPath"]).ToList();

            var result = new Dictionary<string, object>
            {
                { "matches", matches.ToArray() },
                { "activeScene", scene.path }
            };

            return result;
        }
        }

        #endregion Helper Methods

        /// <summary>
        /// Converts a glob pattern to a regex pattern for hierarchy path matching.
        /// </summary>
        private static string ConvertGlobToRegex(string glob)
        {
            var escaped = Regex.Escape(glob);
            
            // Replace escaped ** with recursive pattern (.*?)
            escaped = escaped.Replace(@"\*\*", ".*?");
            
            // Replace escaped * with direct children pattern ([^/]*)
            escaped = escaped.Replace(@"\*", @"[^/]*");
            
            // Anchor to start and end
            return "^" + escaped + "$";
        }

        /// <summary>
        /// Checks if a name matches a pattern (supports wildcards).
        /// </summary>
        private static bool MatchesNamePattern(string name, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return true;

            // Simple wildcard matching: * matches any characters
            var regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$";
            return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Resolves a component type name to a Type object.
        /// Accepts both "BoxCollider" and "UnityEngine.BoxCollider" formats.
        /// </summary>
        private static Type ResolveComponentType(string componentType)
        {
            if (string.IsNullOrEmpty(componentType))
                return null;

            // Try direct type name first
            var type = Type.GetType(componentType);
            if (type != null)
                return type;

            // Try with UnityEngine namespace
            type = Type.GetType($"UnityEngine.{componentType}, UnityEngine");
            if (type != null)
                return type;

            // Try with UnityEngine.CoreModule
            type = Type.GetType($"UnityEngine.{componentType}, UnityEngine.CoreModule");
            if (type != null)
                return type;

            return null;
        }

        /// <summary>
        /// Checks if setting parent would create a circular hierarchy.
        /// </summary>
        private static bool IsCircularHierarchy(Transform child, Transform potentialParent)
        {
            var current = potentialParent;
            while (current != null)
            {
                if (current == child)
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Gets the value of a SerializedProperty as an object.
        /// </summary>
        private static object GetSerializedPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue; break;
                case SerializedPropertyType.Boolean:
                    return property.boolValue; break;
                case SerializedPropertyType.Float:
                    return property.floatValue; break;
                case SerializedPropertyType.String:
                    return property.stringValue; break;
                case SerializedPropertyType.Vector2:
                    return new { x = property.vector2Value.x, y = property.vector2Value.y }; break;
                case SerializedPropertyType.Vector3:
                    return new { x = property.vector3Value.x, y = property.vector3Value.y, z = property.vector3Value.z }; break;
                case SerializedPropertyType.Vector4:
                    return new { x = property.vector4Value.x, y = property.vector4Value.y, z = property.vector4Value.z, w = property.vector4Value.w }; break;
                case SerializedPropertyType.Quaternion:
                    return new { x = property.quaternionValue.x, y = property.quaternionValue.y, z = property.quaternionValue.z, w = property.quaternionValue.w }; break;
                case SerializedPropertyType.Color:
                    return new { r = property.colorValue.r, g = property.colorValue.g, b = property.colorValue.b, a = property.colorValue.a }; break;
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null ? property.objectReferenceValue.name : null; break;
                default:
                    return property.stringValue; // Fallback to string representation
            }
        }

        #endregion
    }

