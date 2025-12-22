# Installation

## Unity Package Manager (Git URL)

### Method 1: Package Manager UI

1. Open Unity Package Manager (Window > Package Manager)
2. Click the `+` button
3. Select "Add package from git URL"
4. Enter: `https://github.com/DatumStudios/EditorMCP.git?path=/package/com.datumstudios.editormcp#main`

### Method 2: manifest.json

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.datumstudios.editormcp": "https://github.com/DatumStudios/EditorMCP.git?path=/package/com.datumstudios.editormcp#main"
  }
}
```

## Version Pinning

To pin to a specific version, append the version tag:

```
https://github.com/DatumStudios/EditorMCP.git?path=/package/com.datumstudios.editormcp#v0.1.2
```

## Requirements

See [Compatibility](./Compatibility.md) for Unity version requirements.

