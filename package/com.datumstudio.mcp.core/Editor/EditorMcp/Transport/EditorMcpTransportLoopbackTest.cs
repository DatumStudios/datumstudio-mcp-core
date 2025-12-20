using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using DatumStudio.Mcp.Core.Editor.Server;

namespace DatumStudio.Mcp.Core.Editor.Transport
{
    /// <summary>
    /// Minimal transport loopback self-test for EditorMCP. Tests the transport layer
    /// using in-memory streams without requiring actual stdio. This is a smoke test,
    /// not a full test harness.
    /// </summary>
    public static class EditorMcpTransportLoopbackTest
    {
        /// <summary>
        /// Runs the transport loopback self-test.
        /// </summary>
        [MenuItem("Tools/DatumStudio/EditorMCP/Run Transport Loopback Self-Test")]
        public static void RunTest()
        {
            try
            {
                // Create in-memory streams for loopback
                var inputStream = new MemoryStream();
                var outputStream = new MemoryStream();

                // Create server and tool registry
                var server = new EditorMcpServer();
                server.Start(); // This registers tools

                // Create router
                var router = new McpMessageRouter(server.ToolRegistry, server.ServerVersion);

                // Create loopback transport with in-memory streams
                var transport = new LoopbackTransport(router, inputStream, outputStream);

                // Start transport
                transport.Start();

                // Send mcp.server.info request using tools/call method (canonical format)
                var requestJson = "{\"jsonrpc\":\"2.0\",\"id\":\"test-001\",\"method\":\"tools/call\",\"params\":{\"tool\":\"mcp.server.info\",\"arguments\":{}}}\n";
                var requestBytes = Encoding.UTF8.GetBytes(requestJson);
                inputStream.Write(requestBytes, 0, requestBytes.Length);
                inputStream.Position = 0; // Reset for reading

                // Wait for response (with timeout)
                var maxWait = 1000; // 1 second max
                var waited = 0;
                while (outputStream.Length == 0 && waited < maxWait)
                {
                    Thread.Sleep(50);
                    waited += 50;
                }

                // Read response
                outputStream.Position = 0;
                var responseBytes = new byte[outputStream.Length];
                outputStream.Read(responseBytes, 0, (int)outputStream.Length);
                var responseJson = Encoding.UTF8.GetString(responseBytes).Trim();

                // Stop transport
                transport.Stop();
                transport.Dispose();
                server.Stop();

                // Parse and validate response
                var isValid = ValidateResponse(responseJson);

                if (isValid)
                {
                    Debug.Log("[EditorMCP Transport Loopback Test] SUCCESS: Transport loopback test passed. Response validated.");
                }
                else
                {
                    Debug.LogError($"[EditorMCP Transport Loopback Test] FAILURE: Response validation failed. Response: {responseJson}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EditorMCP Transport Loopback Test] FAILURE: Exception during test: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates the response JSON contains required fields per mcp.server.info tool output schema:
        /// serverVersion, unityVersion, platform, enabledToolCategories, and tier.
        /// </summary>
        private static bool ValidateResponse(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson))
                return false;

            try
            {
                // Validate JSON-RPC envelope structure
                var hasJsonRpc = responseJson.Contains("\"jsonrpc\"");
                var hasId = responseJson.Contains("\"id\"");
                var hasResult = responseJson.Contains("\"result\"");

                if (!hasJsonRpc || !hasId || !hasResult)
                    return false;

                // Validate result contains tool output structure
                var hasTool = responseJson.Contains("\"tool\"") && responseJson.Contains("mcp.server.info");
                var hasOutput = responseJson.Contains("\"output\"");

                if (!hasTool || !hasOutput)
                    return false;

                // Validate mcp.server.info tool output schema fields (per Core_Tools_v0.1.md)
                var hasServerVersion = responseJson.Contains("\"serverVersion\"");
                var hasUnityVersion = responseJson.Contains("\"unityVersion\"");
                var hasPlatform = responseJson.Contains("\"platform\"");
                var hasEnabledToolCategories = responseJson.Contains("\"enabledToolCategories\"");
                var hasTier = responseJson.Contains("\"tier\"");

                return hasServerVersion && hasUnityVersion && hasPlatform && hasEnabledToolCategories && hasTier;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Loopback transport that uses in-memory streams instead of stdio.
        /// </summary>
        private class LoopbackTransport : IDisposable
        {
            private readonly McpMessageRouter _router;
            private readonly MemoryStream _inputStream;
            private readonly MemoryStream _outputStream;
            private readonly LineJsonReader _reader;
            private readonly LineJsonWriter _writer;
            private Thread _readThread;
            private bool _isRunning;
            private bool _disposed;

            public LoopbackTransport(McpMessageRouter router, MemoryStream inputStream, MemoryStream outputStream)
            {
                _router = router ?? throw new ArgumentNullException(nameof(router));
                _inputStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
                _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
                
                _reader = new LineJsonReader(_inputStream);
                _writer = new LineJsonWriter(_outputStream);
            }

            public void Start()
            {
                if (_isRunning)
                    return;

                if (_disposed)
                    throw new ObjectDisposedException(nameof(LoopbackTransport));

                _isRunning = true;
                _readThread = new Thread(ReadLoop)
                {
                    IsBackground = true,
                    Name = "EditorMCP Loopback Transport Test"
                };
                _readThread.Start();
            }

            public void Stop()
            {
                if (!_isRunning)
                    return;

                _isRunning = false;

                if (_readThread != null && _readThread.IsAlive)
                {
                    if (!_readThread.Join(TimeSpan.FromSeconds(1)))
                    {
                        // Thread didn't finish in time - continue anyway for test
                    }
                }
            }

            private void ReadLoop()
            {
                try
                {
                    while (_isRunning && !_reader.EndOfStream)
                    {
                        string line = _reader.ReadNextLine();
                        if (line == null)
                            break;

                        try
                        {
                            var request = McpJsonHelper.ParseRequest(line);
                            if (request == null)
                            {
                                SendParseError(null, "Failed to parse request JSON");
                                continue;
                            }

                            var response = _router.Route(request);
                            string responseJson = McpJsonBuilder.BuildResponse(response);
                            _writer.WriteLine(responseJson);
                        }
                        catch (Exception ex)
                        {
                            SendParseError(null, $"JSON parse error: {ex.Message}");
                        }
                    }
                }
                catch
                {
                    // Test transport - ignore errors
                }
            }

            private void SendParseError(object id, string message)
            {
                try
                {
                    var errorResponse = new McpResponse
                    {
                        JsonRpc = "2.0",
                        Id = id,
                        Error = new McpError
                        {
                            Code = JsonRpcErrorCodes.ParseError,
                            Message = message,
                            Data = new Dictionary<string, object>()
                        }
                    };
                    string errorJson = McpJsonBuilder.BuildResponse(errorResponse);
                    _writer.WriteLine(errorJson);
                }
                catch
                {
                    // Ignore
                }
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    Stop();
                    _reader?.Dispose();
                    _writer?.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}

