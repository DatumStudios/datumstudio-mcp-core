# Installation

## Unity Package Manager (Git URL)

### Method 1: Package Manager UI

1. Open Unity Package Manager (Window > Package Manager)
2. Click the `+` button
3. Select "Add package from git URL"
4. Enter: `https://github.com/DatumStudios/datumstudio-mcp-core.git?path=package/com.datumstudio.mcp.core`

### Method 2: manifest.json

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.datumstudio.mcp.core": "https://github.com/DatumStudios/datumstudio-mcp-core.git?path=package/com.datumstudio.mcp.core"
  }
}
```

## Version Pinning

To pin to a specific version, append the version tag:

```
https://github.com/DatumStudios/datumstudio-mcp-core.git?path=package/com.datumstudio.mcp.core#v0.1.0
```

## Requirements

See [Compatibility](./Compatibility.md) for Unity version requirements.

