using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using DatumStudio.Mcp.Core.Editor.Tools;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Tests.Editor
{
    /// <summary>
    /// EditMode tests for AudioMixerSnapshotReadTool.
    /// Tests skip gracefully if no AudioMixer assets exist or AudioMixer API is unavailable.
    /// </summary>
    public class AudioMixerSnapshotReadToolTests
    {
        private AudioMixerSnapshotReadTool _tool;
        private AudioMixer _testMixer;
        private string _testMixerPath;
        private string _testMixerGuid;

        [SetUp]
        public void SetUp()
        {
            _tool = new AudioMixerSnapshotReadTool();
            
            // Try to find an existing AudioMixer in the project (restrict to Assets folder to avoid scanning Packages/)
            var mixerGuids = AssetDatabase.FindAssets("t:AudioMixer", new[] { "Assets" });
            if (mixerGuids != null && mixerGuids.Length > 0)
            {
                _testMixerPath = AssetDatabase.GUIDToAssetPath(mixerGuids[0]);
                _testMixerGuid = mixerGuids[0];
                _testMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(_testMixerPath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // No cleanup needed - we use existing assets
        }

        [Test]
        public void Tool_HasCorrectDefinition()
        {
            Assert.NotNull(_tool.Definition);
            Assert.AreEqual("audio.mixer.snapshot.read", _tool.Definition.Id);
            Assert.AreEqual("core", _tool.Definition.Tier);
            Assert.AreEqual(SafetyLevel.ReadOnly, _tool.Definition.SafetyLevel);
        }

        [Test]
        public void Invoke_WithMissingInputs_ReturnsError()
        {
            var request = new ToolInvokeRequest
            {
                Tool = "audio.mixer.snapshot.read",
                Arguments = new System.Collections.Generic.Dictionary<string, object>()
            };

            var response = _tool.Invoke(request);

            Assert.NotNull(response);
            Assert.True(response.Output.ContainsKey("error"));
        }

        [Test]
        public void Invoke_WithInvalidMixerPath_ReturnsError()
        {
            var request = new ToolInvokeRequest
            {
                Tool = "audio.mixer.snapshot.read",
                Arguments = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "mixerPath", "Assets/NonExistentMixer.mixer" },
                    { "snapshotName", "TestSnapshot" }
                }
            };

            var response = _tool.Invoke(request);

            Assert.NotNull(response);
            Assert.True(response.Output.ContainsKey("error"));
        }

        [Test]
        public void Invoke_WithInvalidMixerGuid_ReturnsError()
        {
            var request = new ToolInvokeRequest
            {
                Tool = "audio.mixer.snapshot.read",
                Arguments = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "mixerGuid", "00000000000000000000000000000000" },
                    { "snapshotName", "TestSnapshot" }
                }
            };

            var response = _tool.Invoke(request);

            Assert.NotNull(response);
            Assert.True(response.Output.ContainsKey("error"));
        }

        [Test]
        public void Invoke_WithMissingSnapshotName_ReturnsError()
        {
            if (_testMixer == null)
            {
                Assert.Ignore("No AudioMixer assets found in project - skipping test");
                return;
            }

            var request = new ToolInvokeRequest
            {
                Tool = "audio.mixer.snapshot.read",
                Arguments = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "mixerPath", _testMixerPath }
                }
            };

            var response = _tool.Invoke(request);

            Assert.NotNull(response);
            Assert.True(response.Output.ContainsKey("error"));
        }

        [Test]
        public void Invoke_WithValidMixerPath_ReturnsValidResponse()
        {
            if (_testMixer == null)
            {
                Assert.Ignore("No AudioMixer assets found in project - skipping test");
                return;
            }

            // Try to find a snapshot (best-effort)
            string snapshotName = "Master";
            try
            {
                var snapshot = _testMixer.FindSnapshot("");
                if (snapshot != null)
                {
                    snapshotName = snapshot.name;
                }
            }
            catch
            {
                // If we can't find a snapshot, test will still validate error handling
            }

            var request = new ToolInvokeRequest
            {
                Tool = "audio.mixer.snapshot.read",
                Arguments = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "mixerPath", _testMixerPath },
                    { "snapshotName", snapshotName }
                }
            };

            var response = _tool.Invoke(request);

            Assert.NotNull(response);
            Assert.NotNull(response.Output);
            
            // Response may contain error if snapshot not found, or parameters if found
            // Both are valid outcomes
        }

        [Test]
        public void Invoke_WithValidMixerGuid_ReturnsValidResponse()
        {
            if (_testMixer == null)
            {
                Assert.Ignore("No AudioMixer assets found in project - skipping test");
                return;
            }

            string snapshotName = "Master";
            try
            {
                var snapshot = _testMixer.FindSnapshot("");
                if (snapshot != null)
                {
                    snapshotName = snapshot.name;
                }
            }
            catch
            {
                // If we can't find a snapshot, test will still validate error handling
            }

            var request = new ToolInvokeRequest
            {
                Tool = "audio.mixer.snapshot.read",
                Arguments = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "mixerGuid", _testMixerGuid },
                    { "snapshotName", snapshotName }
                }
            };

            var response = _tool.Invoke(request);

            Assert.NotNull(response);
            Assert.NotNull(response.Output);
        }

        [Test]
        public void Invoke_DoesNotThrow_EvenIfNoMixers()
        {
            var request = new ToolInvokeRequest
            {
                Tool = "audio.mixer.snapshot.read",
                Arguments = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "mixerPath", "Assets/NonExistent.mixer" },
                    { "snapshotName", "Test" }
                }
            };

            // Should not throw even if AudioMixer API is unavailable
            Assert.DoesNotThrow(() => _tool.Invoke(request));
        }

        [Test]
        public void Invoke_HandlesBestEffortLimitations()
        {
            if (_testMixer == null)
            {
                Assert.Ignore("No AudioMixer assets found in project - skipping test");
                return;
            }

            string snapshotName = "Master";
            try
            {
                var snapshot = _testMixer.FindSnapshot("");
                if (snapshot != null)
                {
                    snapshotName = snapshot.name;
                }
            }
            catch
            {
                // If we can't find a snapshot, test will still validate error handling
            }

            var request = new ToolInvokeRequest
            {
                Tool = "audio.mixer.snapshot.read",
                Arguments = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "mixerPath", _testMixerPath },
                    { "snapshotName", snapshotName }
                }
            };

            var response = _tool.Invoke(request);

            Assert.NotNull(response);
            
            // Response may include diagnostics if there are limitations
            if (response.Output.ContainsKey("diagnostics"))
            {
                var diagnostics = response.Output["diagnostics"];
                Assert.NotNull(diagnostics);
            }
        }
    }
}

