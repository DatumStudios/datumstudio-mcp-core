using System;
using System.IO;
using System.Threading;
using UnityEngine;
using DatumStudios.EditorMCP.Schemas;

namespace DatumStudios.EditorMCP.Transport
{
    /// <summary>
    /// Handles stdio-based transport for MCP communication. Reads line-delimited JSON
    /// from stdin and writes responses to stdout. Runs in a background thread to avoid
    /// blocking the Unity Editor main thread.
    /// </summary>
    public class StdioTransport : IDisposable
    {
        private readonly McpMessageRouter _router;
        private readonly LineJsonReader _reader;
        private readonly LineJsonWriter _writer;
        private Thread _readThread;
        private bool _isRunning;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the StdioTransport class.
        /// </summary>
        /// <param name="router">The message router to handle requests.</param>
        public StdioTransport(McpMessageRouter router)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            
            // Use standard input/output streams
            _reader = new LineJsonReader(Console.OpenStandardInput());
            _writer = new LineJsonWriter(Console.OpenStandardOutput());
        }

        /// <summary>
        /// Starts the transport and begins reading from stdin in a background thread.
        /// </summary>
        public void Start()
        {
            if (_isRunning)
                return;

            if (_disposed)
                throw new ObjectDisposedException(nameof(StdioTransport));

            _isRunning = true;
            _readThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "EditorMCP StdioTransport Read Thread"
            };
            _readThread.Start();
        }

        /// <summary>
        /// Stops the transport and stops reading from stdin.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            // Wait for read thread to finish (with timeout)
            if (_readThread != null && _readThread.IsAlive)
            {
                if (!_readThread.Join(TimeSpan.FromSeconds(2)))
                {
                    // Thread didn't finish in time - log warning but continue
                    Debug.LogWarning("StdioTransport read thread did not stop within timeout.");
                }
            }
        }

        /// <summary>
        /// Background thread loop that reads lines from stdin and processes them.
        /// </summary>
        private void ReadLoop()
        {
            try
            {
                while (_isRunning && !_reader.EndOfStream)
                {
                    string line = _reader.ReadNextLine();
                    if (line == null)
                    {
                        // End of stream
                        break;
                    }

                    // Parse JSON into McpRequest
                    try
                    {
                        var request = McpJsonHelper.ParseRequest(line);
                        if (request == null)
                        {
                            // Invalid JSON - send parse error
                            SendParseError(null, "Failed to parse request JSON");
                            continue;
                        }

                        // Route request and get response
                        var response = _router.Route(request);

                        // Write response as JSON string
                        string responseJson = McpJsonBuilder.BuildResponse(response);
                        _writer.WriteLine(responseJson);
                    }
                    catch (Exception ex)
                    {
                        // JSON parse error - send parse error response
                        SendParseError(null, $"JSON parse error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Transport error - log but don't crash
                Debug.LogError($"StdioTransport read loop error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a parse error response.
        /// </summary>
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
                        Data = new System.Collections.Generic.Dictionary<string, object>()
                    }
                };
                string errorJson = McpJsonBuilder.BuildResponse(errorResponse);
                _writer.WriteLine(errorJson);
            }
            catch
            {
                // If we can't even send an error, there's nothing we can do
                // Fail closed: don't throw, just log
                Debug.LogError("Failed to send parse error response");
            }
        }

        /// <summary>
        /// Disposes the transport and releases resources.
        /// </summary>
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

