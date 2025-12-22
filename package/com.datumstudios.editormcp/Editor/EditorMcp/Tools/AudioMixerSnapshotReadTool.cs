using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using DatumStudios.EditorMCP.Schemas;

namespace DatumStudios.EditorMCP.Tools
{
    /// <summary>
    /// Tool: audio.mixer.snapshot.read - Returns the parameter values for a specific AudioMixer snapshot.
    /// </summary>
    public class AudioMixerSnapshotReadTool : IEditorMcpTool
    {
        /// <summary>
        /// Gets the tool definition.
        /// </summary>
        public ToolDefinition Definition { get; }

        /// <summary>
        /// Initializes a new instance of the AudioMixerSnapshotReadTool class.
        /// </summary>
        public AudioMixerSnapshotReadTool()
        {
            Definition = CreateDefinition();
        }

        /// <summary>
        /// Invokes the tool to read snapshot parameters.
        /// </summary>
        /// <param name="request">The tool invocation request with mixerGuid or mixerPath and snapshotName.</param>
        /// <returns>Snapshot parameters response.</returns>
        public ToolInvokeResponse Invoke(ToolInvokeRequest request)
        {
            string mixerPath = null;
            string mixerGuid = null;
            string snapshotName = null;

            if (request.Arguments != null)
            {
                if (request.Arguments.TryGetValue("mixerPath", out var mixerPathObj) && mixerPathObj is string)
                {
                    mixerPath = (string)mixerPathObj;
                }

                if (request.Arguments.TryGetValue("mixerGuid", out var mixerGuidObj) && mixerGuidObj is string)
                {
                    mixerGuid = (string)mixerGuidObj;
                }

                if (request.Arguments.TryGetValue("snapshotName", out var snapshotNameObj) && snapshotNameObj is string)
                {
                    snapshotName = (string)snapshotNameObj;
                }
            }

            // Validate input
            if (string.IsNullOrEmpty(mixerPath) && string.IsNullOrEmpty(mixerGuid))
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", "Either 'mixerPath' or 'mixerGuid' must be provided" }
                    }
                };
            }

            if (string.IsNullOrEmpty(snapshotName))
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", "'snapshotName' is required" }
                    }
                };
            }

            // Convert GUID to path if needed
            if (!string.IsNullOrEmpty(mixerGuid))
            {
                mixerPath = AssetDatabase.GUIDToAssetPath(mixerGuid);
                if (string.IsNullOrEmpty(mixerPath))
                {
                    return new ToolInvokeResponse
                    {
                        Tool = Definition.Id,
                        Output = new Dictionary<string, object>
                        {
                            { "error", $"AudioMixer with GUID '{mixerGuid}' not found" }
                        }
                    };
                }
            }

            // Guard: Only process assets in Assets/ folder (never touch Packages/)
            if (string.IsNullOrEmpty(mixerPath) || !mixerPath.StartsWith("Assets/"))
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", $"AudioMixer path must be in Assets/ folder. Package assets are not supported." }
                    }
                };
            }

            // Load mixer
            AudioMixer mixer = null;
            try
            {
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
            }
            catch (System.Exception ex)
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", $"Failed to load AudioMixer: {ex.Message}" }
                    }
                };
            }

            if (mixer == null)
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", $"AudioMixer at path '{mixerPath}' not found or is not an AudioMixer asset" }
                    }
                };
            }

            // Find snapshot
            AudioMixerSnapshot snapshot = null;
            try
            {
                // Try to find snapshot by name
                snapshot = FindSnapshotByName(mixer, snapshotName);
            }
            catch (System.Exception ex)
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", $"Failed to find snapshot: {ex.Message}" },
                        { "diagnostics", "Unity AudioMixer API may have limitations accessing snapshots" }
                    }
                };
            }

            if (snapshot == null)
            {
                return new ToolInvokeResponse
                {
                    Tool = Definition.Id,
                    Output = new Dictionary<string, object>
                    {
                        { "error", $"Snapshot '{snapshotName}' not found in mixer '{mixerPath}'" }
                    }
                };
            }

            // Read parameters (best-effort)
            var parameters = new List<Dictionary<string, object>>();
            var diagnostics = new List<string>();

            try
            {
                // Get exposed parameters from the mixer
                var exposedParams = GetExposedParameters(mixer);
                
                // Read snapshot parameter values using SerializedObject
                var snapshotSo = new SerializedObject(snapshot);
                var snapshotValuesProperty = snapshotSo.FindProperty("m_SnapshotValues");
                
                if (snapshotValuesProperty != null && snapshotValuesProperty.isArray)
                {
                    // Build a map of parameter GUID to value
                    var paramValueMap = new Dictionary<string, float>();
                    
                    for (int i = 0; i < snapshotValuesProperty.arraySize; i++)
                    {
                        var valueElement = snapshotValuesProperty.GetArrayElementAtIndex(i);
                        var guidProperty = valueElement.FindPropertyRelative("guid");
                        var valueProperty = valueElement.FindPropertyRelative("value");
                        
                        if (guidProperty != null && valueProperty != null)
                        {
                            paramValueMap[guidProperty.stringValue] = valueProperty.floatValue;
                        }
                    }
                    
                    // Match exposed parameters with their values
                    var exposedParamsWithGuids = GetExposedParametersWithGuids(mixer);
                    
                    foreach (var paramInfo in exposedParamsWithGuids)
                    {
                        var paramName = paramInfo.Key;
                        var paramGuid = paramInfo.Value;
                        
                        if (paramValueMap.TryGetValue(paramGuid, out float value))
                        {
                            // Try to determine which group this parameter belongs to
                            string groupName = GetParameterGroup(mixer, paramName);
                            
                            parameters.Add(new Dictionary<string, object>
                            {
                                { "name", paramName },
                                { "value", value },
                                { "group", groupName ?? "Unknown" }
                            });
                        }
                        else
                        {
                            diagnostics.Add($"Parameter '{paramName}' exists but value not found in snapshot");
                        }
                    }
                }
                else
                {
                    diagnostics.Add("Snapshot values property not found - Unity API limitation");
                }

                // Sort for deterministic ordering
                parameters = parameters.OrderBy(p => (string)p["group"]).ThenBy(p => (string)p["name"]).ToList();
            }
            catch (System.Exception ex)
            {
                diagnostics.Add($"Error reading parameters: {ex.Message}");
            }

            var output = new Dictionary<string, object>
            {
                { "mixerPath", mixerPath },
                { "snapshotName", snapshotName },
                { "parameters", parameters.ToArray() }
            };

            // Add note to diagnostics if there are any issues
            if (diagnostics.Count > 0)
            {
                diagnostics.Add("Some parameter values may not be available due to Unity API limitations. This is a best-effort read operation.");
            }

            var response = new ToolInvokeResponse
            {
                Tool = Definition.Id,
                Output = output,
                Diagnostics = diagnostics.Count > 0 ? diagnostics.ToArray() : null
            };

            return response;
        }

        private AudioMixerSnapshot FindSnapshotByName(AudioMixer mixer, string snapshotName)
        {
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
                        var snapshot = snapshotElement.objectReferenceValue as AudioMixerSnapshot;
                        if (snapshot != null && snapshot.name == snapshotName)
                        {
                            return snapshot;
                        }
                    }
                }
            }
            catch
            {
                // Fallback: try FindSnapshot (may not work for all cases)
                var snapshot = mixer.FindSnapshot(snapshotName);
                if (snapshot != null && snapshot.name == snapshotName)
                {
                    return snapshot;
                }
            }

            return null;
        }

        private List<string> GetExposedParameters(AudioMixer mixer)
        {
            var parameters = new List<string>();

            try
            {
                // Use SerializedObject to get exposed parameters
                var so = new SerializedObject(mixer);
                var exposedParamsProperty = so.FindProperty("m_ExposedParameters");
                
                if (exposedParamsProperty != null && exposedParamsProperty.isArray)
                {
                    for (int i = 0; i < exposedParamsProperty.arraySize; i++)
                    {
                        var paramElement = exposedParamsProperty.GetArrayElementAtIndex(i);
                        var nameProperty = paramElement.FindPropertyRelative("name");
                        if (nameProperty != null)
                        {
                            parameters.Add(nameProperty.stringValue);
                        }
                    }
                }
            }
            catch
            {
                // Best-effort: if we can't get exposed parameters, return empty list
            }

            return parameters;
        }

        private Dictionary<string, string> GetExposedParametersWithGuids(AudioMixer mixer)
        {
            var parameters = new Dictionary<string, string>();

            try
            {
                // Use SerializedObject to get exposed parameters with their GUIDs
                var so = new SerializedObject(mixer);
                var exposedParamsProperty = so.FindProperty("m_ExposedParameters");
                
                if (exposedParamsProperty != null && exposedParamsProperty.isArray)
                {
                    for (int i = 0; i < exposedParamsProperty.arraySize; i++)
                    {
                        var paramElement = exposedParamsProperty.GetArrayElementAtIndex(i);
                        var nameProperty = paramElement.FindPropertyRelative("name");
                        var guidProperty = paramElement.FindPropertyRelative("guid");
                        
                        if (nameProperty != null && guidProperty != null)
                        {
                            parameters[nameProperty.stringValue] = guidProperty.stringValue;
                        }
                    }
                }
            }
            catch
            {
                // Best-effort: if we can't get exposed parameters, return empty dictionary
            }

            return parameters;
        }

        private string GetParameterGroup(AudioMixer mixer, string parameterName)
        {
            try
            {
                // Try to find which group this parameter belongs to
                // This is best-effort as Unity API doesn't directly expose this
                var groups = mixer.FindMatchingGroups("");
                if (groups != null)
                {
                    foreach (var group in groups)
                    {
                        // Check if parameter name contains group name (heuristic)
                        if (parameterName.Contains(group.name))
                        {
                            return group.name;
                        }
                    }
                }
            }
            catch
            {
                // Best-effort: return null if we can't determine
            }

            return null;
        }

        private ToolDefinition CreateDefinition()
        {
            return new ToolDefinition
            {
                Id = "audio.mixer.snapshot.read",
                Name = "Read Audio Mixer Snapshot",
                Description = "Returns the parameter values for a specific AudioMixer snapshot. Shows structured, numeric tooling without mutation.",
                Category = "audio",
                SafetyLevel = SafetyLevel.ReadOnly,
                Tier = "core",
                SchemaVersion = "0.1.0",
                Inputs = new Dictionary<string, ToolParameterSchema>
                {
                    {
                        "mixerPath",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "Path to the AudioMixer asset. Either mixerPath or mixerGuid must be provided."
                        }
                    },
                    {
                        "mixerGuid",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = false,
                            Description = "GUID of the AudioMixer asset. Either mixerPath or mixerGuid must be provided."
                        }
                    },
                    {
                        "snapshotName",
                        new ToolParameterSchema
                        {
                            Type = "string",
                            Required = true,
                            Description = "Name of the snapshot to read"
                        }
                    }
                },
                Outputs = new Dictionary<string, ToolOutputSchema>
                {
                    {
                        "mixerPath",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Path to the AudioMixer asset"
                        }
                    },
                    {
                        "snapshotName",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Name of the snapshot that was read"
                        }
                    },
                    {
                        "parameters",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "List of exposed parameters and their values",
                            Items = new ToolOutputSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, ToolOutputSchema>
                                {
                                    {
                                        "name",
                                        new ToolOutputSchema { Type = "string", Description = "Parameter name" }
                                    },
                                    {
                                        "value",
                                        new ToolOutputSchema { Type = "number", Description = "Parameter value" }
                                    },
                                    {
                                        "group",
                                        new ToolOutputSchema { Type = "string", Description = "Mixer group name (best-effort)" }
                                    }
                                }
                            }
                        }
                    },
                    {
                        "diagnostics",
                        new ToolOutputSchema
                        {
                            Type = "array",
                            Description = "Diagnostic messages about limitations or issues encountered",
                            Items = new ToolOutputSchema { Type = "string" }
                        }
                    },
                    {
                        "note",
                        new ToolOutputSchema
                        {
                            Type = "string",
                            Description = "Note about best-effort limitations"
                        }
                    }
                },
                Notes = "Read-only. Reads snapshot parameter values only; no mixer parameters or snapshots are modified. Best-effort: Unity API limitations may prevent reading all parameter values. Diagnostics field will indicate any issues encountered."
            };
        }
    }
}

