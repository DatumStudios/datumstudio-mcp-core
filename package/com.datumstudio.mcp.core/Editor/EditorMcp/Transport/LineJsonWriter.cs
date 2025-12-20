using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DatumStudio.Mcp.Core.Editor.Transport
{
    /// <summary>
    /// Writes line-delimited JSON to a stream. Writes exactly one JSON object per line
    /// and flushes immediately for real-time communication.
    /// </summary>
    public class LineJsonWriter : IDisposable
    {
        private readonly StreamWriter _writer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the LineJsonWriter class.
        /// </summary>
        /// <param name="stream">The output stream to write to.</param>
        public LineJsonWriter(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            _writer = new StreamWriter(stream, Encoding.UTF8, 4096, true)
            {
                AutoFlush = true // Flush immediately for real-time communication
            };
        }

        /// <summary>
        /// Writes a JSON object as a single line, followed by a newline.
        /// </summary>
        /// <param name="obj">The object to serialize and write.</param>
        /// <exception cref="ArgumentNullException">Thrown when obj is null.</exception>
        /// <exception cref="ArgumentException">Thrown when serialization fails.</exception>
        public void WriteLine(object obj)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LineJsonWriter));

            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            try
            {
                // Try JsonUtility first (works for simple objects)
                string json = JsonUtility.ToJson(obj, prettyPrint: false);
                _writer.WriteLine(json);
                _writer.Flush(); // Ensure immediate write
            }
            catch
            {
                // If JsonUtility fails, try to write as string (for already-serialized JSON)
                if (obj is string jsonString)
                {
                    _writer.WriteLine(jsonString);
                    _writer.Flush();
                }
                else
                {
                    throw new ArgumentException($"Failed to serialize object of type {obj.GetType().Name}");
                }
            }
        }

        /// <summary>
        /// Writes a JSON string directly as a line.
        /// </summary>
        /// <param name="json">The JSON string to write.</param>
        /// <exception cref="ArgumentNullException">Thrown when json is null.</exception>
        public void WriteLine(string json)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LineJsonWriter));

            if (json == null)
                throw new ArgumentNullException(nameof(json));

            _writer.WriteLine(json);
            _writer.Flush(); // Ensure immediate write
        }

        /// <summary>
        /// Flushes the underlying stream.
        /// </summary>
        public void Flush()
        {
            if (!_disposed)
            {
                _writer?.Flush();
            }
        }

        /// <summary>
        /// Disposes the writer and underlying stream.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _disposed = true;
            }
        }
    }
}

