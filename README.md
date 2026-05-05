# DeILize

Remove IL-related information from .NET assembly properties (in runtime and file).

## Features

- **Runtime PE header patching** — Zeroes CLR data directory in in-memory PE headers so Process Hacker / System Informer cannot detect .NET metadata.
- **Debug directory hiding** — Zeroes debug directory in memory.
- **PEB module list unlink** — Removes module entries from the PEB LDR linked list (opt-in, risky).
- **Harmony hooks** — Automatically patches newly loaded assemblies via Harmony postfixes.
- **Assembly renaming** — Rewrites assembly identity and references using Mono.Cecil.
- **Fody/Costura support** — Detects and patches embedded PE resources (including Costura compressed).
- **File patching** — Strips assembly attributes, debug info, and optionally zeroes CLR directory (destructive mode).
- **CLI tool** — `patch-file`, `rename-assembly`, `inspect` commands.

## Usage

```csharp
// Runtime patch all loaded assemblies
DeILizeRuntime.HideCurrentAppDomain();

// Install hooks for future assembly loads
DeILizeRuntime.InstallHooks(new RuntimePatchOptions
{
    HideModuleFromPeb = true,
    HideAssemblyDebugInfo = true
});

// File patching
DeILizeRuntime.PatchAssemblyFile("input.dll", "output.dll", new FilePatchOptions
{
    Destructive = true,
    StripDebugInfo = true,
    RemoveAssemblyAttributes = true
});
```

## CLI

```
DeILize.CLI patch-file <input> <output> [--destructive] [--strip-debug]
DeILize.CLI rename-assembly <input> <output> --name <new-name>
DeILize.CLI inspect <input>
```

## Requirements

- .NET Framework 4.8
- Windows only
