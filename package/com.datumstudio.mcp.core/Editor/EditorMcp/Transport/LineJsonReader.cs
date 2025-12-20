using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DatumStudio.Mcp.Core.Editor.Transport
{
    /// <summary>
    /// Reads line-delimited JSON from a stream. Handles UTF-8 encoding, empty lines,
    /// max line length guards, and JSON parse errors gracefully.
    /// </summary>
    public class LineJsonReader : IDisposable
    {
        private readonly StreamReader _reader;
        private readonly int _maxLineLength;
        private bool _disposed;

        /// <summary>
        /// Maximum line length in bytes (default: 1MB).
        /// </summary>
        public const int DefaultMaxLineLength = 1024 * 1024;

        /// <summary>
        /// Initializes a new instance of the LineJsonReader class.
        /// </summary>
        /// <param name="stream">The input stream to read from.</param>
        /// <param name="maxLineLength">Maximum line length in bytes. Default: 1MB.</param>
        public LineJsonReader(Stream stream, int maxLineLength = DefaultMaxLineLength)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            _reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true);
            _maxLineLength = maxLineLength;
        }

        /// <summary>
        /// Reads the next line-delimited JSON object from the stream.
        /// </summary>
        /// <returns>The raw JSON line as a string, or null if end of stream or empty line.</returns>
        /// <exception cref="InvalidOperationException">Thrown when line exceeds max length.</exception>
        public string ReadNextLine()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LineJsonReader));

            string line = _reader.ReadLine();
            if (line == null)
                return null; // End of stream

            // Ignore empty lines
            if (string.IsNullOrWhiteSpace(line))
                return ReadNextLine(); // Recursively read next line

            // Check line length
            int lineLengthBytes = Encoding.UTF8.GetByteCount(line);
            if (lineLengthBytes > _maxLineLength)
            {
                throw new InvalidOperationException($"Line exceeds maximum length of {_maxLineLength} bytes. Received {lineLengthBytes} bytes.");
            }

            return line;
        }

        /// <summary>
        /// Checks if the stream has reached the end.
        /// </summary>
        public bool EndOfStream => _reader.EndOfStream;

        /// <summary>
        /// Disposes the reader and underlying stream.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _reader?.Dispose();
                _disposed = true;
            }
        }
    }
}

