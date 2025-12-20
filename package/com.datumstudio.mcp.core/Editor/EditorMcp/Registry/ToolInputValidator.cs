using System;
using System.Collections.Generic;
using System.Linq;
using DatumStudio.Mcp.Core.Editor.Schemas;

namespace DatumStudio.Mcp.Core.Editor.Registry
{
    /// <summary>
    /// Validates tool input arguments against tool parameter schemas.
    /// Provides deterministic validation with detailed error messages including field paths.
    /// </summary>
    public static class ToolInputValidator
    {
        /// <summary>
        /// Validates input arguments against a tool definition's input schema.
        /// </summary>
        /// <param name="definition">The tool definition containing the input schema.</param>
        /// <param name="arguments">The arguments to validate.</param>
        /// <returns>Validation result with errors if validation failed.</returns>
        public static ValidationResult Validate(ToolDefinition definition, Dictionary<string, object> arguments)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            if (arguments == null)
                arguments = new Dictionary<string, object>();

            var errors = new List<ValidationError>();

            // Check required parameters
            foreach (var input in definition.Inputs)
            {
                var paramName = input.Key;
                var paramSchema = input.Value;

                if (paramSchema.Required && !arguments.ContainsKey(paramName))
                {
                    errors.Add(new ValidationError
                    {
                        FieldPath = paramName,
                        Message = $"Required parameter '{paramName}' is missing."
                    });
                }
            }

            // Validate provided arguments
            foreach (var arg in arguments)
            {
                var paramName = arg.Key;
                var argValue = arg.Value;

                if (!definition.Inputs.TryGetValue(paramName, out var paramSchema))
                {
                    errors.Add(new ValidationError
                    {
                        FieldPath = paramName,
                        Message = $"Unknown parameter '{paramName}'."
                    });
                    continue;
                }

                // Validate the argument value
                var valueErrors = ValidateValue(paramName, argValue, paramSchema);
                errors.AddRange(valueErrors);
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        private static List<ValidationError> ValidateValue(string fieldPath, object value, ToolParameterSchema schema)
        {
            var errors = new List<ValidationError>();

            if (value == null)
            {
                if (schema.Required)
                {
                    errors.Add(new ValidationError
                    {
                        FieldPath = fieldPath,
                        Message = $"Parameter '{fieldPath}' is required but was null."
                    });
                }
                return errors;
            }

            // Type validation
            var typeError = ValidateType(fieldPath, value, schema);
            if (typeError != null)
            {
                errors.Add(typeError);
                return errors; // Don't continue validation if type is wrong
            }

            // Enum validation
            if (schema.Enum != null && schema.Enum.Length > 0)
            {
                var stringValue = value.ToString();
                if (!schema.Enum.Contains(stringValue))
                {
                    errors.Add(new ValidationError
                    {
                        FieldPath = fieldPath,
                        Message = $"Parameter '{fieldPath}' must be one of: {string.Join(", ", schema.Enum)}. Got: {stringValue}."
                    });
                }
            }

            // Numeric range validation
            if (schema.Type == "integer" || schema.Type == "number")
            {
                if (TryGetNumericValue(value, out var numericValue))
                {
                    if (schema.Minimum.HasValue && numericValue < schema.Minimum.Value)
                    {
                        errors.Add(new ValidationError
                        {
                            FieldPath = fieldPath,
                            Message = $"Parameter '{fieldPath}' must be >= {schema.Minimum.Value}. Got: {numericValue}."
                        });
                    }

                    if (schema.Maximum.HasValue && numericValue > schema.Maximum.Value)
                    {
                        errors.Add(new ValidationError
                        {
                            FieldPath = fieldPath,
                            Message = $"Parameter '{fieldPath}' must be <= {schema.Maximum.Value}. Got: {numericValue}."
                        });
                    }
                }
            }

            // Nested object validation
            if (schema.Type == "object" && schema.Properties != null && schema.Properties.Count > 0)
            {
                if (value is Dictionary<string, object> dict)
                {
                    foreach (var prop in schema.Properties)
                    {
                        var propName = prop.Key;
                        var propSchema = prop.Value;
                        var nestedPath = $"{fieldPath}.{propName}";

                        if (dict.TryGetValue(propName, out var propValue))
                        {
                            var nestedErrors = ValidateValue(nestedPath, propValue, propSchema);
                            errors.AddRange(nestedErrors);
                        }
                        else if (propSchema.Required)
                        {
                            errors.Add(new ValidationError
                            {
                                FieldPath = nestedPath,
                                Message = $"Required property '{propName}' is missing in object '{fieldPath}'."
                            });
                        }
                    }
                }
            }

            // Array validation
            if (schema.Type == "array" && schema.Items != null)
            {
                if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    int index = 0;
                    foreach (var item in enumerable)
                    {
                        var itemPath = $"{fieldPath}[{index}]";
                        var itemErrors = ValidateValue(itemPath, item, schema.Items);
                        errors.AddRange(itemErrors);
                        index++;
                    }
                }
            }

            return errors;
        }

        private static ValidationError ValidateType(string fieldPath, object value, ToolParameterSchema schema)
        {
            switch (schema.Type)
            {
                case "string":
                    if (!(value is string))
                    {
                        return new ValidationError
                        {
                            FieldPath = fieldPath,
                            Message = $"Parameter '{fieldPath}' must be a string. Got: {value.GetType().Name}."
                        };
                    }
                    break;

                case "integer":
                    if (!IsInteger(value))
                    {
                        return new ValidationError
                        {
                            FieldPath = fieldPath,
                            Message = $"Parameter '{fieldPath}' must be an integer. Got: {value.GetType().Name}."
                        };
                    }
                    break;

                case "number":
                    if (!IsNumber(value))
                    {
                        return new ValidationError
                        {
                            FieldPath = fieldPath,
                            Message = $"Parameter '{fieldPath}' must be a number. Got: {value.GetType().Name}."
                        };
                    }
                    break;

                case "boolean":
                    if (!(value is bool))
                    {
                        return new ValidationError
                        {
                            FieldPath = fieldPath,
                            Message = $"Parameter '{fieldPath}' must be a boolean. Got: {value.GetType().Name}."
                        };
                    }
                    break;

                case "array":
                    if (!(value is System.Collections.IEnumerable) || value is string)
                    {
                        return new ValidationError
                        {
                            FieldPath = fieldPath,
                            Message = $"Parameter '{fieldPath}' must be an array. Got: {value.GetType().Name}."
                        };
                    }
                    break;

                case "object":
                    if (!(value is Dictionary<string, object>))
                    {
                        return new ValidationError
                        {
                            FieldPath = fieldPath,
                            Message = $"Parameter '{fieldPath}' must be an object. Got: {value.GetType().Name}."
                        };
                    }
                    break;
            }

            return null;
        }

        private static bool IsInteger(object value)
        {
            return value is int || value is long || value is short || value is byte ||
                   value is uint || value is ulong || value is ushort || value is sbyte;
        }

        private static bool IsNumber(object value)
        {
            return IsInteger(value) || value is float || value is double || value is decimal;
        }

        private static bool TryGetNumericValue(object value, out double numericValue)
        {
            numericValue = 0;
            if (value is int i) { numericValue = i; return true; }
            if (value is long l) { numericValue = l; return true; }
            if (value is short s) { numericValue = s; return true; }
            if (value is byte b) { numericValue = b; return true; }
            if (value is uint ui) { numericValue = ui; return true; }
            if (value is ulong ul) { numericValue = ul; return true; }
            if (value is ushort us) { numericValue = us; return true; }
            if (value is sbyte sb) { numericValue = sb; return true; }
            if (value is float f) { numericValue = f; return true; }
            if (value is double d) { numericValue = d; return true; }
            if (value is decimal dec) { numericValue = (double)dec; return true; }
            return false;
        }
    }

    /// <summary>
    /// Result of input validation.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets whether validation passed.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets the list of validation errors (empty if valid).
        /// </summary>
        public List<ValidationError> Errors { get; set; }

        public ValidationResult()
        {
            Errors = new List<ValidationError>();
        }
    }

    /// <summary>
    /// A validation error with field path and message.
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// The field path where the error occurred (e.g., "scenePath" or "options.depth").
        /// </summary>
        public string FieldPath { get; set; }

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string Message { get; set; }
    }
}

