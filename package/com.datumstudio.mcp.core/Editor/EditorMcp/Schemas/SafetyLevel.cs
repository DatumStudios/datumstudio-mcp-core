namespace DatumStudio.Mcp.Core.Editor.Schemas
{
    /// <summary>
    /// Defines the safety level of a tool, indicating what operations it can perform.
    /// </summary>
    public enum SafetyLevel
    {
        /// <summary>
        /// Tool only reads data; no modifications possible.
        /// All Core v0.1 tools are read-only.
        /// </summary>
        ReadOnly
    }
}

