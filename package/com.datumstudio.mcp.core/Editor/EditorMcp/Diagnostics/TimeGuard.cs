using System;
using System.Diagnostics;

namespace DatumStudio.Mcp.Core.Editor.Diagnostics
{
    /// <summary>
    /// Provides time-bounded execution guards to prevent operations from hanging.
    /// Used for scanning operations that may take a long time on large projects.
    /// </summary>
    public class TimeGuard
    {
        /// <summary>
        /// Default maximum execution time in milliseconds (30 seconds).
        /// </summary>
        public const int DefaultMaxMilliseconds = 30000;

        /// <summary>
        /// Maximum execution time for asset scanning operations (10 seconds).
        /// </summary>
        public const int AssetScanMaxMilliseconds = 10000;

        /// <summary>
        /// Maximum execution time for scene scanning operations (15 seconds).
        /// </summary>
        public const int SceneScanMaxMilliseconds = 15000;

        private readonly Stopwatch _stopwatch;
        private readonly int _maxMilliseconds;

        /// <summary>
        /// Gets whether the time limit has been exceeded.
        /// </summary>
        public bool IsExceeded => _stopwatch.ElapsedMilliseconds >= _maxMilliseconds;

        /// <summary>
        /// Gets the elapsed time in milliseconds.
        /// </summary>
        public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

        /// <summary>
        /// Initializes a new instance of the TimeGuard class.
        /// </summary>
        /// <param name="maxMilliseconds">Maximum execution time in milliseconds.</param>
        public TimeGuard(int maxMilliseconds = DefaultMaxMilliseconds)
        {
            _maxMilliseconds = maxMilliseconds;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Checks if the time limit has been exceeded and throws if so.
        /// </summary>
        /// <exception cref="TimeoutException">Thrown when time limit is exceeded.</exception>
        public void Check()
        {
            if (IsExceeded)
            {
                throw new TimeoutException($"Operation exceeded time limit of {_maxMilliseconds}ms");
            }
        }

        /// <summary>
        /// Gets a diagnostic message indicating partial results due to time limit.
        /// </summary>
        /// <param name="itemsProcessed">Number of items processed before timeout.</param>
        /// <param name="totalItems">Total number of items (if known).</param>
        /// <returns>Diagnostic message.</returns>
        public string GetPartialResultMessage(int itemsProcessed, int? totalItems = null)
        {
            if (totalItems.HasValue)
            {
                return $"Scan stopped after {ElapsedMilliseconds}ms. Processed {itemsProcessed} of {totalItems.Value} items. Results may be partial.";
            }
            else
            {
                return $"Scan stopped after {ElapsedMilliseconds}ms. Processed {itemsProcessed} items. Results may be partial.";
            }
        }
    }
}

