# Runtime

## Purpose

This folder exists as a **seam** for potential future expansion. In v1, this package is **editor-only** and contains no runtime code.

## Why a Seam?

A seam is a structural placeholder that allows for future extension without breaking existing code structure. If runtime functionality is needed in future versions, this folder provides a clear location for that code.

## Current State

- No runtime code exists in v1
- No assembly definition is provided (not needed without code)
- This folder may remain empty indefinitely

## Future Considerations

If runtime code is added:
- An assembly definition will be created
- Namespaces will follow `DatumStudio.Mcp.Core.*` convention
- Runtime code will be clearly separated from editor code

