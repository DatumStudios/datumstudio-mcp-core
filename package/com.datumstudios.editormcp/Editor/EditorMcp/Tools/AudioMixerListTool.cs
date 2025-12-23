using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using DatumStudios.EditorMCP.Registry;

namespace DatumStudios.EditorMCP.Tools
{
    /// <summary>
    /// Tool: audio.mixer.list - Lists all AudioMixer assets in the project with their groups and snapshots.
    /// </summary>
    [McpToolCategory("audio")]
    public static class AudioMixerListTool
    {
        /// <summary>
        /// Lists all AudioMixer assets in the project with their groups and snapshots. Concrete, familiar example of domain-specific tooling without mutation.
        /// </summary>
        /// <param name="jsonParams">JSON parameters (no parameters required).</param>
        /// <returns>JSON string with AudioMixer list.</returns>
        [McpTool("audio.mixer.list", "Lists all AudioMixer assets in the project with their groups and snapshots. Concrete, familiar example of domain-specific tooling without mutation.", Tier.Core)]
        public static string Invoke(string jsonParams)
        {
            var mixers = new List<Dictionary<string, object>>();

            try
            {
                // Restrict to Assets folder to avoid scanning Packages/ (which causes "no meta file" errors)
                var mixerGuids = AssetDatabase.FindAssets("t:AudioMixer", new[] { "Assets" });
                
                if (mixerGuids != null && mixerGuids.Length > 0)
                {
                    // Sort for deterministic ordering
                    var sortedGuids = mixerGuids.OrderBy(g => g).ToList();

                    foreach (var guid in sortedGuids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        // Guard: Only process assets in Assets/ folder (never touch Packages/)
                        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                            continue;

                        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
                        if (mixer == null)
                            continue;

                        var mixerData = SerializeMixer(mixer, path, guid);
                        mixers.Add(mixerData);
                    }
                }
            }
            catch (System.Exception ex)
            {
                // AudioMixer package might not be available - return empty list with error info
                var errorResult = new Dictionary<string, object>
                {
                    { "mixers", new object[0] },
                    { "error", $"AudioMixer API not available: {ex.Message}" }
                };
                return UnityEngine.JsonUtility.ToJson(errorResult);
            }

            var result = new Dictionary<string, object>
            {
                { "mixers", mixers.ToArray() }
            };

            return UnityEngine.JsonUtility.ToJson(result);
        }

        private static Dictionary<string, object> SerializeMixer(AudioMixer mixer, string path, string guid)
        {
            var groups = new List<Dictionary<string, object>>();
            var snapshots = new List<Dictionary<string, object>>();

            try
            {
                // Get groups
                var groupGuids = mixer.FindMatchingGroups("");
                if (groupGuids != null)
                {
                    // Sort for deterministic ordering
                    var sortedGroups = groupGuids.OrderBy(g => g.name).ToList();

                    foreach (var group in sortedGroups)
                    {
                        // AudioMixerGroup is a runtime object, not an asset, so it doesn't have a GUID
                        // We can only provide the name
                        groups.Add(new Dictionary<string, object>
                        {
                            { "name", group.name }
                        });
                    }
                }

                // Get snapshots
                var snapshotArray = mixer.FindSnapshot("");
                if (snapshotArray != null)
                {
                    // Get all snapshots - Unity API limitation: FindSnapshot returns one snapshot
                    // We need to use reflection or SerializedObject to get all snapshots
                    var allSnapshots = GetAllSnapshots(mixer);
                    
                    // Sort for deterministic ordering
                    var sortedSnapshots = allSnapshots.OrderBy(s => s.name).ToList();

                    foreach (var snapshot in sortedSnapshots)
                    {
                        snapshots.Add(new Dictionary<string, object>
                        {
                            { "name", snapshot.name }
                        });
                    }
                }
            }
            catch
            {
                // Best-effort: if we can't get groups/snapshots, return what we have
            }

            return new Dictionary<string, object>
            {
                { "path", path },
                { "guid", guid },
                { "name", mixer.name },
                { "groups", groups.ToArray() },
                { "snapshots", snapshots.ToArray() }
            };
        }

        private static List<AudioMixerSnapshot> GetAllSnapshots(AudioMixer mixer)
        {
            var snapshots = new List<AudioMixerSnapshot>();

            try
            {
                // Use SerializedObject to access all snapshots
                var so = new SerializedObject(mixer);
                var snapshotsProperty = so.FindProperty("m_Snapshots");
                
                if (snapshotsProperty != null && snapshotsProperty.isArray)
                {
                    for (int i = 0; i < snapshotsProperty.arraySize; i++)
                    {
                        var snapshotElement = snapshotsProperty.GetArrayElementAtIndex(i);
                        var snapshotObject = snapshotElement.objectReferenceValue as AudioMixerSnapshot;
                        if (snapshotObject != null)
                        {
                            snapshots.Add(snapshotObject);
                        }
                    }
                }
            }
            catch
            {
                // Fallback: try to find at least one snapshot
                var snapshot = mixer.FindSnapshot("");
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                }
            }

            return snapshots;
        }
    }
}

