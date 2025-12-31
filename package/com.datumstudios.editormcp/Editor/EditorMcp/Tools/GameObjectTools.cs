// GameObjectTools.cs
// EditorMCP - GameObject related MCP tools
//
// NOTES:
// - Keep tool methods as: public static Dictionary<string, object> ToolName(string jsonParams)
//   because the current ToolRegistry invokes tools via reflection with a single string param.
// - These tools return Dictionary<string, object> for JSON serialization by the pipeline.
// - This file is written to be "compile-first": no fancy polymorphic JSON, no nullable-dynamic tricks.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DatumStudios.EditorMCP.Helpers;
using DatumStudios.EditorMCP.Registry;

namespace DatumStudios.EditorMCP.Tools
{
    [McpToolCategory("go")]
    public static class GameObjectTools
    {
        // ----------------------------
        // DTOs (JsonUtility friendly)
        // ----------------------------

        [Serializable]
        private class FindRequest
        {
            public string? nameContains;
            public string? nameRegex;
            public bool includeInactive = true;
            public bool rootsOnly = false;
            public int? limit;
            public string? sceneName; // optional: restrict to scene by name
        }

        [Serializable]
        private class FindMatch
        {
            public int instanceId;
            public string name = "";
            public string hierarchyPath = "";
            public bool activeInHierarchy;
            public string scene = "";
            public string tag = "";
            public int layer;
        }

        [Serializable]
        private class FindResponse
        {
            public string activeScene = "";
            public int matchCount;
            public FindMatch[] matches = Array.Empty<FindMatch>();
        }

        [Serializable]
        private class SetParentRequest
        {
            public int childInstanceId;
            public int? parentInstanceId; // null => move to root
            public bool worldPositionStays = true;
        }

        [Serializable]
        private class SetParentResponse
        {
            public bool success;
            public string message = "";
            public int childInstanceId;
            public int? parentInstanceId;
            public string childHierarchyPath = "";
        }

        [Serializable]
        private class ComponentListRequest
        {
            public int gameObjectInstanceId;
        }

        [Serializable]
        private class ComponentInfo
        {
            public string type = "";
            public string name = "";
            public string? enabledProperty; // "enabled" if present
        }

        [Serializable]
        private class ComponentListResponse
        {
            public int gameObjectInstanceId;
            public string gameObjectName = "";
            public ComponentInfo[] components = Array.Empty<ComponentInfo>();
        }

        // ----------------------------
        // MCP Tools
        // ----------------------------

        [McpTool("go.find", "Find GameObjects in the active scene (or a named scene) by substring or regex.", Tier.Core)]
        public static Dictionary<string, object> FindGameObjects(string jsonParams)
        {
            var req = SafeFromJson<FindRequest>(jsonParams) ?? new FindRequest();

            Regex? rx = null;
            if (!string.IsNullOrWhiteSpace(req.nameRegex))
            {
                try { rx = new Regex(req.nameRegex, RegexOptions.Compiled); }
                catch
                {
                    // Return an empty result but include info (keep compile-safe)
                    var bad = new FindResponse
                    {
                        activeScene = SceneManager.GetActiveScene().name,
                        matchCount = 0,
                        matches = Array.Empty<FindMatch>()
                    };
                    return new Dictionary<string, object>
                    {
                        { "activeScene", SceneManager.GetActiveScene().name },
                        { "matchCount", 0 },
                        { "matches", Array.Empty<FindMatch>() }
                    };
                }
            }

            Scene scene = ResolveScene(req.sceneName) ?? SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            var results = new List<FindMatch>(64);

            foreach (var root in roots)
            {
                if (req.rootsOnly)
                {
                    if (Matches(root, req.nameContains, rx, req.includeInactive))
                        results.Add(ToMatch(root));
                    continue;
                }

                CollectMatchesRecursive(root.transform, req, rx, results);

                if (req.limit.HasValue && results.Count >= req.limit.Value)
                    break;
            }

            if (req.limit.HasValue && results.Count > req.limit.Value)
                results = results.Take(req.limit.Value).ToList();

            var resp = new FindResponse
            {
                activeScene = SceneManager.GetActiveScene().name,
                matchCount = results.Count,
                matches = results.ToArray()
            };

            return new Dictionary<string, object>
            {
                { "activeScene", resp.activeScene },
                { "matchCount", resp.matchCount },
                { "matches", resp.matches }
            };
        }

        [McpTool("go.setParent", "Reparent a GameObject under another GameObject (or to root).", Tier.Core)]
        public static Dictionary<string, object> SetParent(string jsonParams)
        {
            var req = SafeFromJson<SetParentRequest>(jsonParams);
            if (req == null)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "Invalid request JSON." }
                };
            }

            var child = EditorUtility.InstanceIDToObject(req.childInstanceId) as GameObject;
            if (child == null)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "childInstanceId did not resolve to a GameObject." }
                };
            }

            Transform? newParent = null;
            if (req.parentInstanceId.HasValue)
            {
                var parentGo = EditorUtility.InstanceIDToObject(req.parentInstanceId.Value) as GameObject;
                if (parentGo == null)
                {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "childInstanceId", req.childInstanceId },
                    { "parentInstanceId", req.parentInstanceId },
                    { "childHierarchyPath", HierarchyResolver.FullHierarchyPath(child.transform) },
                    { "message", $"Successfully moved {child.name} to {newParent.name}." }
                };
                }

                newParent = parentGo.transform;

                // Prevent circular parenting.
                if (IsCircularHierarchy(child.transform, newParent))
                {
                    var result = new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "Cannot set parent: would create a circular hierarchy." },
                        { "childInstanceId", req.childInstanceId },
                        { "parentInstanceId", req.parentInstanceId }
                    };
                    return result;
                }
            }

            try
            {
                Undo.RegisterFullObjectHierarchyUndo(child, "EditorMCP Set Parent");
                child.transform.SetParent(newParent, req.worldPositionStays);

                // Mark scene dirty so changes persist.
                EditorSceneManager.MarkSceneDirty(child.scene);

                var ok = new SetParentResponse
                {
                    success = true,
                    message = "OK",
                    childInstanceId = req.childInstanceId,
                    parentInstanceId = req.parentInstanceId,
                    childHierarchyPath = GetHierarchyPath(child)
                };
                return new Dictionary<string, object>
                {
                    { "success", ok.success },
                    { "childInstanceId", ok.childInstanceId },
                    { "parentInstanceId", ok.parentInstanceId },
                    { "childHierarchyPath", ok.childHierarchyPath }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "Failed to move child. Exception occurred." }
                };
            }
        }

        [McpTool("component.list", "List components attached to a GameObject (basic info).", Tier.Core)]
        public static Dictionary<string, object> ListComponents(string jsonParams)
        {
            var req = SafeFromJson<ComponentListRequest>(jsonParams);
            if (req == null)
            {
                return new Dictionary<string, object>
                {
                    { "gameObjectInstanceId", req.gameObjectInstanceId },
                    { "gameObjectName", "" },
                    { "components", Array.Empty<ComponentInfo>() }
                };
            }

            var go = EditorUtility.InstanceIDToObject(req.gameObjectInstanceId) as GameObject;
            if (go == null)
            {
                return new Dictionary<string, object>
                {
                    { "gameObjectInstanceId", req.gameObjectInstanceId },
                    { "gameObjectName", "" },
                    { "components", Array.Empty<ComponentInfo>() }
                };
            }

            var comps = go.GetComponents<Component>();
            var list = new List<ComponentInfo>(comps.Length);

            foreach (var c in comps)
            {
                if (c == null) continue;

                string? enabledProp = null;
                try
                {
                    // Many Unity components have "m_Enabled" or "enabled".
                    var so = new SerializedObject(c);
                    var pEnabled = so.FindProperty("m_Enabled") ?? so.FindProperty("enabled");
                    if (pEnabled != null)
                        enabledProp = pEnabled.propertyPath;
                }
                catch
                {
                    // ignore exceptions when getting component enabled property
                }

                list.Add(new ComponentInfo
                {
                    type = c.GetType().FullName ?? c.GetType().Name,
                    name = c.name,
                    enabledProperty = enabledProp
                });
            }

            var resp = new ComponentListResponse
            {
                gameObjectInstanceId = req.gameObjectInstanceId,
                gameObjectName = go.name,
                components = list.ToArray()
            };

            return new Dictionary<string, object>
            {
                { "gameObjectInstanceId", go.GetInstanceID() },
                { "gameObjectName", go.name },
                { "components", resp.components }
            };
        }

        // ----------------------------
        // Helpers
        // ----------------------------

        private static Scene? ResolveScene(string? sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return null;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && string.Equals(s.name, sceneName, StringComparison.OrdinalIgnoreCase))
                    return s;
            }

            return null;
        }

        private static void CollectMatchesRecursive(Transform root, FindRequest req, Regex? rx, List<FindMatch> results)
        {
            var go = root.gameObject;

            if (Matches(go, req.nameContains, rx, req.includeInactive))
                results.Add(ToMatch(go));

            if (req.limit.HasValue && results.Count >= req.limit.Value)
                return;

            for (int i = 0; i < root.childCount; i++)
            {
                CollectMatchesRecursive(root.GetChild(i), req, rx, results);
                if (req.limit.HasValue && results.Count >= req.limit.Value)
                    return;
            }
        }

        private static bool Matches(GameObject go, string? contains, Regex? rx, bool includeInactive)
        {
            if (!includeInactive && !go.activeInHierarchy)
                return false;

            if (!string.IsNullOrWhiteSpace(contains))
            {
                if (go.name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            if (rx != null && !rx.IsMatch(go.name))
                return false;

            // If neither filter provided, treat as match-all (but still respects includeInactive)
            return true;
        }

        private static FindMatch ToMatch(GameObject go)
        {
            return new FindMatch
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                hierarchyPath = GetHierarchyPath(go),
                activeInHierarchy = go.activeInHierarchy,
                scene = go.scene.IsValid() ? go.scene.name : "",
                tag = go.tag,
                layer = go.layer
            };
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var t = go.transform;
            var parts = new List<string>(16);
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static bool IsCircularHierarchy(Transform child, Transform newParent)
        {
            // If newParent is within child's subtree, circular.
            var t = newParent;
            while (t != null)
            {
                if (t == child) return true;
                t = t.parent;
            }
            return false;
        }

        // JsonUtility cannot reliably deserialize arbitrary/optional fields.
        // This helper is intentionally conservative: if it fails, returns null.
        private static T? SafeFromJson<T>(string json) where T : class
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                // JsonUtility requires a wrapped object with matching field names.
                return JsonUtility.FromJson<T>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
