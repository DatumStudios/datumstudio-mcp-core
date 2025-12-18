using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Tools
{
    /// <summary>
    /// Tool: audio.mixer.list - Lists all AudioMixer assets in the project with their groups and snapshots.
    /// </summary>
    public class AudioMixerListTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the AudioMixerListTool class.
        /// </summary>
        public AudioMixerListTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to list AudioMixers.
        /// </summary>
        /// <param name="request">The tool invocation request (no arguments required).</param>
        /// <returns>AudioMixer list response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
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
                        if (string.IsNullOrEmpty(path))
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
                // AudioMixer package might not be available
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "mixers", new object[0] }
                    },
                    Diagnostics = new[] { $"AudioMixer API not available: {ex.Message}" }
                };
            }

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = new Dictionary<string, object>
                {
                    { "mixers", mixers.ToArray() }
                }
            };

            return response;
        }

        private Dictionary<string, object> SerializeMixer(AudioMixer mixer, string path, string guid)
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
                        groups.Add(new Dictionary<string, object>
                        {
                            { "name", group.name },
                            { "guid", group.assetGUID }
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

        private List<AudioMixerSnapshot> GetAllSnapshots(AudioMixer mixer)
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

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "audio.mixer.list",
                Name = "List Audio Mixers",
                Description = "Lists all AudioMixer assets in the project with their groups and snapshots. Concrete, familiar example of domain-specific tooling without mutation.",
                Category = "audio",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>(),
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "mixers",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "List of AudioMixer assets",
                            Items = new ToolOutputSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, ToolOutputSchema>
                                {
                                    {
                                        "path",
                                        new ToolOutputSchema { Type = "string", Description = "Asset path" }
                                    },
                                    {
                                        "guid",
                                        new ToolOutputSchema { Type = "string", Description = "Asset GUID" }
                                    },
                                    {
                                        "name",
                                        new ToolOutputSchema { Type = "string", Description = "Mixer name" }
                                    },
                                    {
                                        "groups",
                                        new ToolOutputSchema
                                        {
                                            Type = "array",
                                            Description = "List of mixer groups",
                                            Items = new ToolOutputSchema
                                            {
                                                Type = "object",
                                                Properties = new Dictionary<string, ToolOutputSchema>
                                                {
                                                    {
                                                        "name",
                                                        new ToolOutputSchema { Type = "string", Description = "Group name" }
                                                    },
                                                    {
                                                        "guid",
                                                        new ToolOutputSchema { Type = "string", Description = "Group GUID" }
                                                    }
                                                }
                                            }
                                        }
                                    },
                                    {
                                        "snapshots",
                                        new ToolOutputSchema
                                        {
                                            Type = "array",
                                            Description = "List of snapshots",
                                            Items = new ToolOutputSchema
                                            {
                                                Type = "object",
                                                Properties = new Dictionary<string, ToolOutputSchema>
                                                {
                                                    {
                                                        "name",
                                                        new ToolOutputSchema { Type = "string", Description = "Snapshot name" }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                Notes = "Read-only. Reads AudioMixer asset structure only; no mixer settings or snapshots are modified. Best-effort: may have limitations accessing all snapshots depending on Unity API availability."
            };
        }
    }
}

