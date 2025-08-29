using UnityEngine;
using System.Collections.Generic;

namespace WhiskerKing.Player
{
    /// <summary>
    /// Advanced input buffering system for responsive controls in Whisker King
    /// Buffers input for 120ms to allow for responsive gameplay as per PRD specifications
    /// Supports multiple input queuing, priority systems, and frame-perfect timing
    /// </summary>
    public class InputBuffer : MonoBehaviour
    {
        public enum InputType
        {
            Jump,
            Slide,
            Attack,
            Pause,
            Restart
        }

        public enum InputPriority
        {
            Low = 0,
            Normal = 1,
            High = 2,
            Critical = 3
        }

        [System.Serializable]
        public struct BufferedInput
        {
            public InputType type;
            public float timestamp;
            public InputPriority priority;
            public bool consumed;
            public Vector2 contextData; // For directional inputs

            public BufferedInput(InputType inputType, float time, InputPriority inputPriority = InputPriority.Normal, Vector2 context = default)
            {
                type = inputType;
                timestamp = time;
                priority = inputPriority;
                consumed = false;
                contextData = context;
            }

            public float GetAge() => Time.time - timestamp;
            public bool IsValid(float maxAge) => !consumed && GetAge() <= maxAge;
        }

        [Header("Configuration")]
        [SerializeField] private float bufferTime = 0.12f; // 120ms as per PRD
        [SerializeField] private int maxBufferedInputsPerType = 3; // Prevent spam
        [SerializeField] private bool enableInputPriority = true;
        [SerializeField] private bool debugMode = false;

        // Enhanced buffered input storage
        private Dictionary<InputType, List<BufferedInput>> bufferedInputQueues = new Dictionary<InputType, List<BufferedInput>>();

        // Statistics and debug information
        private Dictionary<InputType, int> totalInputCounts = new Dictionary<InputType, int>();
        private Dictionary<InputType, int> consumedInputCounts = new Dictionary<InputType, int>();
        private Dictionary<InputType, float> lastInputTimes = new Dictionary<InputType, float>();
        private float averageInputLatency = 0f;

        #region Initialization

        public void Initialize(float bufferTimeSeconds = 0.12f)
        {
            bufferTime = bufferTimeSeconds;
            
            // Initialize queues for all input types
            foreach (InputType inputType in System.Enum.GetValues(typeof(InputType)))
            {
                bufferedInputQueues[inputType] = new List<BufferedInput>();
                totalInputCounts[inputType] = 0;
                consumedInputCounts[inputType] = 0;
                lastInputTimes[inputType] = -1f;
            }

            Debug.Log($"InputBuffer initialized with {bufferTime * 1000:F0}ms buffer time, max {maxBufferedInputsPerType} inputs per type");
        }

        private void Awake()
        {
            Initialize();
        }

        #endregion

        #region Buffer Input

        /// <summary>
        /// Buffer an input with the current timestamp and optional priority/context
        /// </summary>
        public void BufferInput(InputType inputType, InputPriority priority = InputPriority.Normal, Vector2 contextData = default)
        {
            if (!bufferedInputQueues.ContainsKey(inputType))
            {
                Debug.LogError($"InputBuffer: Unknown input type {inputType}");
                return;
            }

            var inputQueue = bufferedInputQueues[inputType];
            
            // Remove excess inputs if at capacity
            if (inputQueue.Count >= maxBufferedInputsPerType)
            {
                // Remove oldest input
                inputQueue.RemoveAt(0);
                
                if (debugMode)
                {
                    Debug.LogWarning($"InputBuffer: Removed oldest {inputType} input (queue full)");
                }
            }

            // Create and add new buffered input
            var newInput = new BufferedInput(inputType, Time.time, priority, contextData);
            
            if (enableInputPriority)
            {
                // Insert based on priority (higher priority first)
                int insertIndex = inputQueue.Count;
                for (int i = 0; i < inputQueue.Count; i++)
                {
                    if (priority > inputQueue[i].priority)
                    {
                        insertIndex = i;
                        break;
                    }
                }
                inputQueue.Insert(insertIndex, newInput);
            }
            else
            {
                // Add to end of queue
                inputQueue.Add(newInput);
            }

            // Update statistics
            totalInputCounts[inputType]++;
            lastInputTimes[inputType] = Time.time;

            if (debugMode)
            {
                Debug.Log($"Buffered input: {inputType} (priority: {priority}, queue size: {inputQueue.Count})");
            }
        }

        /// <summary>
        /// Buffer an input with the current timestamp (backward compatibility)
        /// </summary>
        public void BufferInput(InputType inputType)
        {
            BufferInput(inputType, InputPriority.Normal, Vector2.zero);
        }

        #endregion

        #region Consume Input

        /// <summary>
        /// Try to consume the highest priority buffered input if it's within the buffer window
        /// Returns true if input was successfully consumed
        /// </summary>
        public bool ConsumeInput(InputType inputType)
        {
            if (!bufferedInputQueues.ContainsKey(inputType))
            {
                return false;
            }

            var inputQueue = bufferedInputQueues[inputType];
            
            // Find the first valid input (highest priority due to sorting)
            for (int i = 0; i < inputQueue.Count; i++)
            {
                var input = inputQueue[i];
                
                if (input.IsValid(bufferTime))
                {
                    // Mark as consumed by removing from queue
                    inputQueue.RemoveAt(i);
                    
                    // Update statistics
                    consumedInputCounts[inputType]++;
                    UpdateInputLatency(input.GetAge());

                    if (debugMode)
                    {
                        Debug.Log($"Consumed input: {inputType} (age: {input.GetAge() * 1000:F0}ms, priority: {input.priority})");
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Try to consume a buffered input and return its context data
        /// </summary>
        public bool ConsumeInput(InputType inputType, out Vector2 contextData)
        {
            contextData = Vector2.zero;

            if (!bufferedInputQueues.ContainsKey(inputType))
            {
                return false;
            }

            var inputQueue = bufferedInputQueues[inputType];
            
            // Find the first valid input
            for (int i = 0; i < inputQueue.Count; i++)
            {
                var input = inputQueue[i];
                
                if (input.IsValid(bufferTime))
                {
                    contextData = input.contextData;
                    
                    // Mark as consumed by removing from queue
                    inputQueue.RemoveAt(i);
                    
                    // Update statistics
                    consumedInputCounts[inputType]++;
                    UpdateInputLatency(input.GetAge());

                    if (debugMode)
                    {
                        Debug.Log($"Consumed input: {inputType} with context {contextData} (age: {input.GetAge() * 1000:F0}ms)");
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if there's a valid buffered input without consuming it
        /// </summary>
        public bool HasBufferedInput(InputType inputType)
        {
            if (!bufferedInputQueues.ContainsKey(inputType))
            {
                return false;
            }

            var inputQueue = bufferedInputQueues[inputType];
            
            // Check if we have any valid inputs
            foreach (var input in inputQueue)
            {
                if (input.IsValid(bufferTime))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get the age of the oldest valid buffered input in seconds
        /// </summary>
        public float GetInputAge(InputType inputType)
        {
            if (!bufferedInputQueues.ContainsKey(inputType))
            {
                return float.MaxValue;
            }

            var inputQueue = bufferedInputQueues[inputType];
            float oldestAge = float.MaxValue;
            
            foreach (var input in inputQueue)
            {
                if (input.IsValid(bufferTime))
                {
                    float age = input.GetAge();
                    if (age < oldestAge)
                    {
                        oldestAge = age;
                    }
                }
            }

            return oldestAge;
        }

        /// <summary>
        /// Get the number of valid buffered inputs for a type
        /// </summary>
        public int GetBufferedInputCount(InputType inputType)
        {
            if (!bufferedInputQueues.ContainsKey(inputType))
            {
                return 0;
            }

            var inputQueue = bufferedInputQueues[inputType];
            int count = 0;
            
            foreach (var input in inputQueue)
            {
                if (input.IsValid(bufferTime))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Update average input latency for performance metrics
        /// </summary>
        private void UpdateInputLatency(float latency)
        {
            // Simple moving average
            averageInputLatency = (averageInputLatency * 0.9f) + (latency * 0.1f);
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Set the buffer time in seconds
        /// </summary>
        public void SetBufferTime(float timeSeconds)
        {
            bufferTime = timeSeconds;
            Debug.Log($"InputBuffer: Buffer time set to {bufferTime * 1000:F0}ms");
        }

        /// <summary>
        /// Get current buffer time in seconds
        /// </summary>
        public float GetBufferTime()
        {
            return bufferTime;
        }

        #endregion

        #region Cleanup

        private void Update()
        {
            CleanupExpiredInputs();
        }

        /// <summary>
        /// Clean up expired buffered inputs
        /// </summary>
        private void CleanupExpiredInputs()
        {
            foreach (var kvp in bufferedInputQueues)
            {
                InputType inputType = kvp.Key;
                var inputQueue = kvp.Value;
                
                // Remove expired inputs
                for (int i = inputQueue.Count - 1; i >= 0; i--)
                {
                    var input = inputQueue[i];
                    
                    if (!input.IsValid(bufferTime))
                    {
                        inputQueue.RemoveAt(i);

                        if (debugMode && input.GetAge() > bufferTime)
                        {
                            Debug.Log($"Expired input: {inputType} (age: {input.GetAge() * 1000:F0}ms)");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clear all buffered inputs
        /// </summary>
        public void ClearAllInputs()
        {
            foreach (var inputQueue in bufferedInputQueues.Values)
            {
                inputQueue.Clear();
            }

            Debug.Log("InputBuffer: All inputs cleared");
        }

        /// <summary>
        /// Clear specific input type
        /// </summary>
        public void ClearInput(InputType inputType)
        {
            if (bufferedInputQueues.ContainsKey(inputType))
            {
                bufferedInputQueues[inputType].Clear();
                
                if (debugMode)
                {
                    Debug.Log($"InputBuffer: Cleared {inputType} inputs");
                }
            }
        }

        /// <summary>
        /// Clear all inputs of a specific priority
        /// </summary>
        public void ClearInputsByPriority(InputPriority priority)
        {
            int totalCleared = 0;

            foreach (var inputQueue in bufferedInputQueues.Values)
            {
                for (int i = inputQueue.Count - 1; i >= 0; i--)
                {
                    if (inputQueue[i].priority == priority)
                    {
                        inputQueue.RemoveAt(i);
                        totalCleared++;
                    }
                }
            }

            if (debugMode)
            {
                Debug.Log($"InputBuffer: Cleared {totalCleared} inputs with priority {priority}");
            }
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (debugMode)
            {
                GUILayout.BeginArea(new Rect(10, 220, 400, 200));
                GUILayout.Label("=== INPUT BUFFER DEBUG ===");
                GUILayout.Label($"Buffer Time: {bufferTime * 1000:F0}ms");
                GUILayout.Label($"Avg Latency: {averageInputLatency * 1000:F1}ms");
                GUILayout.Label($"Priority System: {(enableInputPriority ? "ON" : "OFF")}");
                GUILayout.Space(5);

                foreach (var kvp in bufferedInputQueues)
                {
                    InputType inputType = kvp.Key;
                    var inputQueue = kvp.Value;
                    
                    int validCount = GetBufferedInputCount(inputType);
                    int totalCount = totalInputCounts.ContainsKey(inputType) ? totalInputCounts[inputType] : 0;
                    int consumedCount = consumedInputCounts.ContainsKey(inputType) ? consumedInputCounts[inputType] : 0;
                    float successRate = totalCount > 0 ? (consumedCount / (float)totalCount) * 100f : 0f;

                    string status = validCount > 0 ? $"ACTIVE ({validCount})" : "EMPTY";
                    GUILayout.Label($"{inputType}: {status} | Total: {totalCount} | Success: {successRate:F0}%");
                }

                GUILayout.EndArea();
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Get comprehensive debug information about the input buffer
        /// </summary>
        public string GetDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== INPUT BUFFER STATUS ===");
            sb.AppendLine($"Buffer Time: {bufferTime * 1000:F0}ms");
            sb.AppendLine($"Max Queue Size: {maxBufferedInputsPerType}");
            sb.AppendLine($"Priority System: {(enableInputPriority ? "Enabled" : "Disabled")}");
            sb.AppendLine($"Average Latency: {averageInputLatency * 1000:F2}ms");
            sb.AppendLine();

            foreach (var kvp in bufferedInputQueues)
            {
                InputType inputType = kvp.Key;
                var inputQueue = kvp.Value;
                
                int validCount = GetBufferedInputCount(inputType);
                int totalCount = totalInputCounts[inputType];
                int consumedCount = consumedInputCounts[inputType];
                float successRate = totalCount > 0 ? (consumedCount / (float)totalCount) * 100f : 0f;
                float lastInputAge = lastInputTimes[inputType] >= 0 ? Time.time - lastInputTimes[inputType] : -1f;

                sb.AppendLine($"{inputType}:");
                sb.AppendLine($"  Valid: {validCount} | Queue: {inputQueue.Count} | Total: {totalCount}");
                sb.AppendLine($"  Consumed: {consumedCount} | Success Rate: {successRate:F1}%");
                
                if (lastInputAge >= 0)
                {
                    sb.AppendLine($"  Last Input: {lastInputAge:F2}s ago");
                }

                if (validCount > 0)
                {
                    sb.AppendLine($"  Oldest Valid: {GetInputAge(inputType) * 1000:F0}ms");
                    
                    // Show detailed queue info
                    for (int i = 0; i < inputQueue.Count && i < 3; i++) // Show first 3 entries
                    {
                        var input = inputQueue[i];
                        if (input.IsValid(bufferTime))
                        {
                            sb.AppendLine($"    [{i}] {input.priority} - {input.GetAge() * 1000:F0}ms");
                        }
                    }
                    
                    if (inputQueue.Count > 3)
                    {
                        sb.AppendLine($"    ... and {inputQueue.Count - 3} more");
                    }
                }
                
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get input statistics for performance monitoring
        /// </summary>
        public Dictionary<string, float> GetInputStatistics()
        {
            var stats = new Dictionary<string, float>
            {
                ["BufferTimeMS"] = bufferTime * 1000f,
                ["AverageLatencyMS"] = averageInputLatency * 1000f,
                ["MaxQueueSize"] = maxBufferedInputsPerType
            };

            foreach (var kvp in totalInputCounts)
            {
                InputType inputType = kvp.Key;
                int total = kvp.Value;
                int consumed = consumedInputCounts[inputType];
                float successRate = total > 0 ? (consumed / (float)total) * 100f : 0f;

                stats[$"{inputType}_Total"] = total;
                stats[$"{inputType}_Consumed"] = consumed;
                stats[$"{inputType}_SuccessRate"] = successRate;
                stats[$"{inputType}_Buffered"] = GetBufferedInputCount(inputType);
            }

            return stats;
        }

        /// <summary>
        /// Reset all statistics (for testing/benchmarking)
        /// </summary>
        public void ResetStatistics()
        {
            foreach (InputType inputType in System.Enum.GetValues(typeof(InputType)))
            {
                totalInputCounts[inputType] = 0;
                consumedInputCounts[inputType] = 0;
                lastInputTimes[inputType] = -1f;
            }
            
            averageInputLatency = 0f;
            
            Debug.Log("InputBuffer: Statistics reset");
        }

        #endregion
    }
}
