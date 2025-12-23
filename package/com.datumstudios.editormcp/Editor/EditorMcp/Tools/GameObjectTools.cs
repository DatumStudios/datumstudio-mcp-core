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
        public static string FindGameObjects(string jsonParams)
        {
            // Check for active scene
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded)
            {
                return UnityEngine.JsonUtility.ToJson(new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", "No active scene loaded" }
                });
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

                    if (paramsObj.TryGetValue("namePattern", out var namePatternObj) && namePatternObj is string)
                    {
                        namePattern = (string)namePatternObj;
                    }

                    if (paramsObj.TryGetValue("maxResults", out var maxResultsObj))
                    {
                        if (maxResultsObj is int)
                        {
                            maxResults = (int)maxResultsObj;
                        }
                        else if (maxResultsObj is long)
                        {
                            maxResults = (int)(long)maxResultsObj;
                        }
                    }
                }
            }

            // Get all GameObjects in the scene
            var allObjects = HierarchyResolver.GetAllGameObjects(scene, includeInactive: false).ToList();
            var matches = new List<Dictionary<string, object>>();

            // Convert glob pattern to regex if hierarchyPath is provided
            Regex hierarchyRegex = null;
            if (!string.IsNullOrEmpty(hierarchyPath))
            {
                try
                {
                    var regexPattern = ConvertGlobToRegex(hierarchyPath);
                    hierarchyRegex = new Regex(regexPattern, RegexOptions.Compiled);
                }
                catch
                {
                    return UnityEngine.JsonUtility.ToJson(new Dictionary<string, object>
                    {
                        { "success", false },
                        { "error", "Invalid hierarchy path pattern" }
                    });
                }
            }

            // Resolve component type if specified
            Type componentTypeObj = null;
            if (!string.IsNullOrEmpty(componentType))
            {
                componentTypeObj = ResolveComponentType(componentType);
                if (componentTypeObj == null)
                {
                    // Component type not found, but don't fail - just skip component filter
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
                });
            }

            // Sort by hierarchy path for deterministic output
            matches = matches.OrderBy(m => m["hierarchyPath"] as string).ToList();

            var result = new Dictionary<string, object>
            {
                { "matches", matches.ToArray() },
                { "activeScene", scene.path }
            };

            return UnityEngine.JsonUtility.ToJson(result);
        }

        /// <summary>
        /// Changes the parent of a GameObject in the active scene.
        /// Enables basic hierarchy management for single-object operations.
        /// </summary>
        /// <param name="jsonParams">JSON parameters with "childPath" (required) and optional "parentPath".</param>
        /// <returns>JSON string with operation result.</returns>
        [McpTool("go.setParent", "Changes the parent of a GameObject in the active scene. Enables basic hierarchy management for single-object operations.", Tier.Core)]
        public static string SetParent(string jsonParams)
        {
            // Check for active scene
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded)
            {
                return UnityEngine.JsonUtility.ToJson(new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", "No active scene loaded" }
                });
            }

            // Parse JSON parameters
            string childPath = null;
            string parentPath = null;

            if (!string.IsNullOrEmpty(jsonParams) && jsonParams != "{}")
            {
                var paramsObj = UnityEngine.JsonUtility.FromJson<Dictionary<string, object>>(jsonParams);
                if (paramsObj != null)
                {
                    if (paramsObj.TryGetValue("childPath", out var childPathObj) && childPathObj is string)
                    {
                        childPath = (string)childPathObj;
                    }

                    if (paramsObj.TryGetValue("parentPath", out var parentPathObj) && parentPathObj is string)
                    {
                        parentPath = (string)parentPathObj;
                    }
                }
            }

            if (string.IsNullOrEmpty(childPath))
            {
                return UnityEngine.JsonUtility.ToJson(new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", "childPath is required" }
                });
            }

            // Find child GameObject
            var child = HierarchyResolver.FindByPath(childPath);
            if (child == null)
            {
                return UnityEngine.JsonUtility.ToJson(new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", "Child GameObject not found" }
                });
            }

            // Find parent GameObject (if specified)
            Transform newParent = null;
            string newParentPath = null;
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parentObj = HierarchyResolver.FindByPath(parentPath);
                if (parentObj == null)
                {
                    return UnityEngine.JsonUtility.ToJson(new Dictionary<string, object>
                    {
                        { "success", false },
                        { "error", "Parent GameObject not found" }
                    });
                }

                // Check for circular hierarchy
                if (IsCircularHierarchy(child.transform, parentObj.transform))
                {
                    return UnityEngine.JsonUtility.ToJson(new Dictionary<string, object>
                    {
                        { "success", false },
                        { "error", "Cannot set parent to descendant" }
                    });
                }

                newParent = parentObj.transform;
                newParentPath = parentPath;
            }
            else
            {
                // Reparent to scene root
                newParentPath = "";
            }

            // Perform reparenting with undo support
            using (var undo = new UndoScope("go.setParent"))
            {
                Undo.SetTransformParent(child.transform, newParent, "EditorMCP: Set Parent");
            }

            var newHierarchyPath = newParent != null 
                ? HierarchyResolver.FullHierarchyPath(child.transform)
                : child.name;

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "childPath", childPath },
                { "newParentPath", newParentPath },
                { "newHierarchyPath", newHierarchyPath }
            };

            return UnityEngine.JsonUtility.ToJson(result);
        }

        /// <summary>
        /// Returns all components attached to a GameObject in the active scene, including component types and property counts.
        /// Perfect complement to go.find for "What components on this?" queries.
        /// </summary>
        /// <param name="jsonParams">JSON parameters with "hierarchyPath" (required) and optional "includeProperties" boolean.</param>
        /// <returns>JSON string with components list.</returns>
        [McpTool("component.list", "Returns all components attached to a GameObject in the active scene, including component types and property counts. Perfect complement to go.find for \"What components on this?\" queries.", Tier.Core)]
        public static string ListComponents(string jsonParams)
        {
            // Check for active scene
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded)
            {
                return UnityEngine.JsonUtility.ToJson(new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", "No active scene loaded" }
                });
            }

            // Parse JSON parameters
            string hierarchyPath = null;
            bool includeProperties = false;

            if (!string.IsNullOrEmpty(jsonParams) && jsonParams != "{}")
            {
                var paramsObj = UnityEngine.JsonUtility.FromJson<Dictionary<string, object>>(jsonParams);
                if (paramsObj != null)
                {
                    if (paramsObj.TryGetValue("hierarchyPath", out var hierarchyPathObj) && hierarchyPathObj is string)
                    {
                        hierarchyPath = (string)hierarchyPathObj;
                    }

                    if (paramsObj.TryGetValue("includeProperties", out var includePropertiesObj))
                    {
                        if (includePropertiesObj is bool)
                        {
                            includeProperties = (bool)includePropertiesObj;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(hierarchyPath))
            {
                return UnityEngine.JsonUtility.ToJson(new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", "hierarchyPath is required" }
                });
            }

            // Find GameObject
            var gameObject = HierarchyResolver.FindByPath(hierarchyPath);
            if (gameObject == null)
            {
                return UnityEngine.JsonUtility.ToJson(new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", $"GameObject with path '{hierarchyPath}' not found" }
                });
            }

            // Get all components
            var components = new List<Dictionary<string, object>>();
            var allComponents = gameObject.GetComponents<Component>();

            foreach (var component in allComponents)
            {
                if (component == null)
                {
                    components.Add(new Dictionary<string, object>
                    {
                        { "type", "Missing Script" },
                        { "instanceId", 0 },
                        { "propertyCount", 0 }
                    });
                    continue;
                }

                var componentData = new Dictionary<string, object>
                {
                    { "type", component.GetType().FullName },
                    { "instanceId", component.GetInstanceID() }
                };

                // Get property count
                int propertyCount = 0;
                if (includeProperties)
                {
                    var serializedFields = new List<Dictionary<string, object>>();
                    try
                    {
                        var serializedObject = new SerializedObject(component);
                        var iterator = serializedObject.GetIterator();
                        iterator.Next(true); // Skip script reference

                        int count = 0;
                        while (iterator.NextVisible(false) && count < 50) // Limit to 50 properties
                        {
                            var fieldData = new Dictionary<string, object>
                            {
                                { "name", iterator.name },
                                { "type", iterator.type }
                            };

                            // Get value (simplified - just store as string representation for now)
                            try
                            {
                                fieldData["value"] = GetSerializedPropertyValue(iterator);
                            }
                            catch
                            {
                                fieldData["value"] = null;
                            }

                            serializedFields.Add(fieldData);
                            propertyCount++;
                            count++;
                        }
                    }
                    catch
                    {
                        // If serialization fails, just count properties
                    }

                    componentData["serializedFields"] = serializedFields.ToArray();
                }
                else
                {
                    // Just count properties without serializing
                    try
                    {
                        var serializedObject = new SerializedObject(component);
                        var iterator = serializedObject.GetIterator();
                        iterator.Next(true); // Skip script reference
                        while (iterator.NextVisible(false))
                        {
                            propertyCount++;
                        }
                    }
                    catch
                    {
                        propertyCount = 0;
                    }
                }

                componentData["propertyCount"] = propertyCount;
                components.Add(componentData);
            }

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "hierarchyPath", hierarchyPath },
                { "components", components.ToArray() },
                { "activeScene", scene.path }
            };

            return UnityEngine.JsonUtility.ToJson(result);
        }

        #region Helper Methods

        /// <summary>
        /// Converts a glob pattern to a regex pattern for hierarchy path matching.
        /// </summary>
        private static string ConvertGlobToRegex(string glob)
        {
            // Escape special regex characters first
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
                    return property.intValue;
                case SerializedPropertyType.Boolean:
                    return property.boolValue;
                case SerializedPropertyType.Float:
                    return property.floatValue;
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Vector2:
                    return new { x = property.vector2Value.x, y = property.vector2Value.y };
                case SerializedPropertyType.Vector3:
                    return new { x = property.vector3Value.x, y = property.vector3Value.y, z = property.vector3Value.z };
                case SerializedPropertyType.Vector4:
                    return new { x = property.vector4Value.x, y = property.vector4Value.y, z = property.vector4Value.z, w = property.vector4Value.w };
                case SerializedPropertyType.Quaternion:
                    return new { x = property.quaternionValue.x, y = property.quaternionValue.y, z = property.quaternionValue.z, w = property.quaternionValue.w };
                case SerializedPropertyType.Color:
                    return new { r = property.colorValue.r, g = property.colorValue.g, b = property.colorValue.b, a = property.colorValue.a };
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null ? property.objectReferenceValue.name : null;
                default:
                    return property.stringValue; // Fallback to string representation
            }
        }

        #endregion
    }
}

