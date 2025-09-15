# How the .NET Tool packaging process works

Much like the [npm packages](https://github.com/azure/azure-mcp/blob/main/eng/npm/README.md), the Azure MCP server is published as a .NET Tool that supports both standard framework-dependent packages and platform-specific Native AOT packages.

The repository supports two distinct packaging modes:

## Standard .NET Tool Packages (Framework-Dependent)

For standard, portable .NET tools that work across platforms with the .NET runtime installed:

```bash
dotnet pack
```

This creates:
* A framework-dependent tool package that works on any platform with .NET installed
* No `RuntimeIdentifiers` specified (RID-agnostic)
* Standard JIT compilation at runtime
* Smaller package size, but requires .NET runtime on target machine

## Native AOT Tool Packages (Platform-Specific)

The .NET Tools feature also supports Native AOT platform-specific packages (setting `<PublishAot>` to true), but because the .NET Toolchain does not support cross-platform AOT compilation, individual platform-specific packages must be built on each platform, often through a CI/CD system's ability to matrix across platforms.

To build Native AOT packages, use the `BuildNative` property:

```bash
dotnet pack -r <runtime identifier> /p:BuildNative=true
```

In most cases, you can rely on the .NET SDK to fill in the appropriate RID for the current host by using:

```bash
dotnet pack --use-current-runtime /p:BuildNative=true
```

This creates:
* A platform-specific tool package with the .NET runtime included
* `RuntimeIdentifiers` are automatically set when `BuildNative=true`
* Native AOT compilation with trimming
* Larger package size, but no runtime dependency on target machine

## Configuration Details

The dual packaging approach is configured in `Directory.Build.props`:

### For all server projects:
- `<PackAsTool>true</PackAsTool>` - Enables .NET tool packaging
- `<IsPackable>true</IsPackable>` - Allows the project to be packed

### For Native AOT builds only (when `BuildNative=true`):
- `<RuntimeIdentifiers>` - Set to support multiple platforms (win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64)
- `<PublishAot>true</PublishAot>` - Enables Native AOT compilation
- `<PublishTrimmed>true</PublishTrimmed>` - Enables trimming to reduce size
- `<SelfContained>true</SelfContained>` - Includes the runtime in the package

### For standard builds (when `BuildNative` is not set):
- No `RuntimeIdentifiers` specified - Creates portable, framework-dependent packages
- `PublishAot=false`, `PublishTrimmed=false`, `SelfContained=false` - Standard JIT behavior

## Wrapper Packages

In addition, you will need to create the 'wrapper' package separately via the following command:

```
dotnet pack
```

Once you have all N packages you can publish them to feeds as you would any package.

## Summary

- **Standard tools**: Use `dotnet pack` for portable, framework-dependent tools
- **Native AOT tools**: Use `dotnet pack -r <RID> /p:BuildNative=true` for platform-specific, self-contained tools
- The same project file supports both scenarios through conditional MSBuild properties