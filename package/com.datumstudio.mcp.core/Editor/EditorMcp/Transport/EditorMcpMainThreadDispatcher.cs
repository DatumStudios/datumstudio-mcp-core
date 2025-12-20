using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Transport
{
    /// <summary>
    /// Dispatches tool invocations to the Unity main thread. Ensures all Unity API calls
    /// execute on the main thread, even when requests arrive from background transport threads.
    /// Uses EditorApplication.delayCall for queued execution (no Update() loops).
    /// </summary>
    public class EditorMcpMainThreadDispatcher
    {
        private static EditorMcpMainThreadDispatcher _instance;
        private readonly Queue<PendingWorkItem> _workQueue = new Queue<PendingWorkItem>();
        private readonly object _queueLock = new object();
        private bool _isProcessing;
        private static int _mainThreadId;

        /// <summary>
        /// Gets the singleton instance of the dispatcher.
        /// </summary>
        public static EditorMcpMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new EditorMcpMainThreadDispatcher();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initializes a new instance of the EditorMcpMainThreadDispatcher class.
        /// </summary>
        private EditorMcpMainThreadDispatcher()
        {
            // Capture the main thread ID during initialization
            // This should be called from the Unity main thread (via Instance getter on main thread)
            // If called from background thread, we'll capture that thread ID (which is incorrect but won't crash)
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            
            // Note: In Unity Editor, the main thread is typically thread ID 1, but this is not guaranteed.
            // The dispatcher will work correctly as long as it's initialized on the main thread.
            // TODO: Consider using Unity's SynchronizationContext or a more robust main thread detection method.
        }

        /// <summary>
        /// Invokes work on the Unity main thread and waits for completion (with timeout).
        /// This method can be called from any thread. The work function will execute on the main thread.
        /// </summary>
        /// <param name="work">The work function to execute on the main thread.</param>
        /// <param name="timeout">Maximum time to wait for completion. Default: 30 seconds.</param>
        /// <returns>The tool invocation response, or a timeout error response if the timeout is exceeded.</returns>
        public ToolInvokeResponse Invoke(Func<ToolInvokeResponse> work, TimeSpan? timeout = null)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            var timeoutValue = timeout ?? TimeSpan.FromSeconds(30);

            // Always use the queue to ensure deterministic execution on main thread
            // Even if we think we're on the main thread, queueing ensures proper ordering
            // and avoids potential race conditions

            // Enqueue work for main thread execution
            var workItem = new PendingWorkItem
            {
                Work = work,
                CompletionEvent = new ManualResetEventSlim(false),
                Result = null,
                Exception = null
            };

            lock (_queueLock)
            {
                _workQueue.Enqueue(workItem);
                
                // Schedule processing if not already scheduled
                if (!_isProcessing)
                {
                    _isProcessing = true;
                    EditorApplication.delayCall += ProcessNextWorkItem;
                }
            }

            // Wait for completion (with timeout)
            bool completed = workItem.CompletionEvent.Wait(timeoutValue);

            if (!completed)
            {
                // Timeout - remove from queue if still pending
                lock (_queueLock)
                {
                    // Try to remove if still in queue (best effort)
                    var items = new List<PendingWorkItem>(_workQueue);
                    items.Remove(workItem);
                    _workQueue.Clear();
                    foreach (var item in items)
                    {
                        _workQueue.Enqueue(item);
                    }
                }

                return CreateTimeoutResponse(timeoutValue);
            }

            // Check for exception
            if (workItem.Exception != null)
            {
                return CreateErrorResponse($"Tool execution failed: {workItem.Exception.Message}", workItem.Exception);
            }

            // Return result
            return workItem.Result ?? CreateErrorResponse("Tool execution returned null result", null);
        }

        /// <summary>
        /// Processes the next work item from the queue. Called via EditorApplication.delayCall.
        /// </summary>
        private void ProcessNextWorkItem()
        {
            if (!IsMainThread())
            {
                // This should never happen, but guard against it
                UnityEngine.Debug.LogError("ProcessNextWorkItem called off main thread!");
                return;
            }

            PendingWorkItem workItem = null;

            lock (_queueLock)
            {
                if (_workQueue.Count == 0)
                {
                    _isProcessing = false;
                    return;
                }

                workItem = _workQueue.Dequeue();
            }

            // Execute work on main thread
            try
            {
                workItem.Result = workItem.Work();
            }
            catch (Exception ex)
            {
                workItem.Exception = ex;
            }
            finally
            {
                // Signal completion
                workItem.CompletionEvent.Set();
            }

            // Schedule next item if queue is not empty
            lock (_queueLock)
            {
                if (_workQueue.Count > 0)
                {
                    EditorApplication.delayCall += ProcessNextWorkItem;
                }
                else
                {
                    _isProcessing = false;
                }
            }
        }

        /// <summary>
        /// Checks if the current thread is the Unity main thread.
        /// </summary>
        private static bool IsMainThread()
        {
            // Compare against the captured main thread ID
            return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        /// <summary>
        /// Creates an error response for tool execution failures.
        /// </summary>
        private static ToolInvokeResponse CreateErrorResponse(string message, Exception exception)
        {
            return new ToolInvokeResponse
            {
                Tool = "internal.error",
                Output = new Dictionary<string, object>
                {
                    { "error", message },
                    { "exceptionType", exception?.GetType().Name ?? "Unknown" }
                },
                Diagnostics = exception != null ? new[] { exception.StackTrace } : null
            };
        }

        /// <summary>
        /// Creates a timeout error response.
        /// </summary>
        private static ToolInvokeResponse CreateTimeoutResponse(TimeSpan timeout)
        {
            return new ToolInvokeResponse
            {
                Tool = "internal.timeout",
                Output = new Dictionary<string, object>
                {
                    { "error", "Tool execution timed out" },
                    { "timeoutSeconds", timeout.TotalSeconds }
                },
                Diagnostics = new[] { $"Tool execution exceeded timeout of {timeout.TotalSeconds} seconds" }
            };
        }

        /// <summary>
        /// Represents a pending work item in the queue.
        /// </summary>
        private class PendingWorkItem
        {
            public Func<ToolInvokeResponse> Work { get; set; }
            public ManualResetEventSlim CompletionEvent { get; set; }
            public ToolInvokeResponse Result { get; set; }
            public Exception Exception { get; set; }
        }
    }
}

