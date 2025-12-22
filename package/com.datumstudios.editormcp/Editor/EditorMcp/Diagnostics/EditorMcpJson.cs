using System;
using UnityEngine;
using DatumStudios.EditorMCP.Schemas;

namespace DatumStudios.EditorMCP.Diagnostics
{
    /// <summary>
    /// Central JSON serialization and deserialization helpers for EditorMCP.
    /// Provides consistent JSON handling with proper error handling.
    /// </summary>
    public static class EditorMcpJson
    {
        /// <summary>
        /// Serializes an object to JSON string.
        /// </summary>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>JSON string representation of the object.</returns>
        /// <exception cref="ArgumentException">Thrown when serialization fails.</exception>
        public static string Serialize<T>(T obj)
        {
            try
            {
                return JsonUtility.ToJson(obj, prettyPrint: true);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to serialize object of type {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deserializes a JSON string to an object.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize to.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>Deserialized object.</returns>
        /// <exception cref="ArgumentException">Thrown when deserialization fails.</exception>
        public static T Deserialize<T>(string json)
        {
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to deserialize JSON to type {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Attempts to deserialize a JSON string to an object.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize to.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <param name="result">The deserialized object, or default if deserialization failed.</param>
        /// <returns>True if deserialization succeeded, false otherwise.</returns>
        public static bool TryDeserialize<T>(string json, out T result)
        {
            result = default(T);
            try
            {
                result = JsonUtility.FromJson<T>(json);
                return result != null;
            }
            catch
            {
                return false;
            }
        }
    }
}

