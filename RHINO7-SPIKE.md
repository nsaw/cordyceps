# Rhino 7 spike

A minimal Rhino 7 / net48 build of Cordyceps, scoped to the write-code / read-error loop on
GhPython nodes. Not a full port. Upstream is Rhino 8 / .NET 8; see
[brookstalley/cordyceps#28](https://github.com/brookstalley/cordyceps/issues/28) for the
net48 question.

## What is in scope

`gh_canvas`, `gh_wire`, `gh_script`, `gh_inspect`, `gh_document`. The Rhino-side tools
(`rhino_scene`, `rhino_render`) are removed — not deferred, not wanted for this use case.

## Changes from upstream

| Area | Change | Why |
|---|---|---|
| `Cordyceps.csproj` | `net8.0` → `net48`, `LangVersion 9.0` | Rhino 7 runs .NET Framework 4.8 (Windows) / Mono (macOS). C# 9 is needed for `or`/type patterns and target-typed conditionals — all compiler-only, no runtime shims. Records and init-only setters would need `IsExternalInit`; the codebase uses neither. |
| `Cordyceps.csproj` | `Grasshopper` 8.0.23304.9001 → 7.13.21348.13001 | Rhino 7 SDK. |
| `Cordyceps.csproj` | `Microsoft.NETFramework.ReferenceAssemblies.net48`: `ExcludeAssets="all"` → `PrivateAssets="all"` | `ExcludeAssets="all"` suppressed the package's build targets, which are what set `TargetFrameworkRootPath`. Without them the net48 targeting pack cannot be found on macOS/Linux (MSB3644). |
| `Cordyceps.csproj` | Added `Microsoft.CSharp` reference | `dynamic` call sites bind through `Microsoft.CSharp.RuntimeBinder`, which is not referenced by default on net48. |
| `Cordyceps.csproj` | Backslash paths → forward slashes | The WinForms reference and the releases-copy target used Windows-only separators. |
| `McpServer.cs`, `Core/JsonRpcEnvelope.cs`, `Core/JsonTypeConverter.cs` | System.Text.Json → Newtonsoft | **See below.** |
| `Core/ComponentRegistry.cs` | Script aliases → `GhPython Script`, GUID `410755B1-224A-4C1E-A407-BF32FB45EA7E` | Rhino 7 has no "Python 3 Script". Identity read from the `GuidAttribute` on `ZuiPythonComponent` in the shipped `GhPython.gha` 7.38.24338.17002: `Name="GhPython Script"`, `NickName="Python"`, `Category="Math"`, `SubCategory="Script"`. |
| `Core/ComponentRegistry.cs` | `Contains(s, StringComparison)` → `IndexOf(...) >= 0` | That overload does not exist on net48. |
| `Tools/Unified/GhScriptTool.cs` | Added `TrySetScriptSource` cascade | **See below.** |
| `Core/ScriptDirective.cs` | Removed | Rhino 8's unified Script component infers language from a `#!` first line. Rhino 7's GhPython carries a concrete language, so there is nothing to preserve. |

## Why Newtonsoft instead of System.Text.Json

net48's BCL has no `System.Text.Json`, so it would have to come from NuGet. Its net48 build
depends on `System.Memory` ≥ 4.0.1.2 and `System.Runtime.CompilerServices.Unsafe` 6.0.0.0.

Rhino 7 **already loads older versions of both into the process** for Roslyn — verified in
`GrasshopperPlugin.rhp/Components/`:

```
System.Memory                          = 4.0.1.1
System.Buffers                         = 4.0.3.0
System.Runtime.CompilerServices.Unsafe = 4.0.6.0
```

.NET Framework binds by exact assembly version, and a `.gha` cannot inject binding redirects
into the host process. So System.Text.Json would fail at load with a `FileLoadException`.
Newtonsoft is a single self-contained assembly with no polyfill dependencies, and was already
a dependency of this project.

Confirmed on the built artifact — its only assembly references are:

```
GH_IO, Grasshopper, Microsoft.CSharp, mscorlib, Newtonsoft.Json,
RhinoCommon, System, System.Core, System.Drawing, System.Windows.Forms
```

Nothing that can collide with what Rhino preloads.

All 56 upstream unit tests for `JsonRpcEnvelope` and `JsonTypeConverter` pass against the
rewrite, including the lossless id echo, the absent-vs-explicit-null id distinction, and the
whole-valued-double coercion. Only the tests' `Parse` helper was ported to `JToken`; no
assertion was changed.

## Why the write path needed a cascade

`GhScriptTool.ActionSet` called `scriptComp.SetSource(finalSource)` through `dynamic`. Rhino 7's
GhPython component has no `SetSource`. Its source is settable through a public `Code` property —
verified from the shipped `GhPython.gha` IL:

```
.method public hidebysig specialname instance void set_Code(string 'value')
  // throws InvalidOperationException("Cannot assign to code while code parameter exists")
  //   when HiddenCodeInput is false
  // sets m_inner_codeInput, then nulls m_compiled_py to force a recompile
```

`TrySetScriptSource` now tries `SetSource(string)` first, then falls back to `Code`, checking
`HiddenCodeInput` **before** assigning so a component with a visible code input returns an
actionable message instead of an opaque exception. This mirrors the probe cascade
`TryGetScriptSource` already used on the read side, and makes the write path equally defensive
on Rhino 8 for third-party script components that lack `SetSource`.

Note `gh_inspect(action='reports')` needed **no** changes: it filters on component type names
containing "Python" and reads the output param named `out`, both of which match Rhino 7's
GhPython component as shipped.

## Build

```bash
dotnet build src/Cordyceps/Cordyceps.csproj -c Release
```

Requires the .NET SDK (no Developer Pack needed — the reference assemblies come from NuGet).
`-c Release` is mandatory; the csproj hard-errors on Debug.

Deploy the resulting `releases/Cordyceps.gha` to:

```
~/Library/Application Support/McNeel/Rhinoceros/7.0/Plug-ins/Grasshopper (b45a29b1-...)/Libraries/
```

then `xattr -dr com.apple.quarantine <path>` and restart Rhino.

## Status

Builds clean and deploys. The in-Rhino round trip (add a GhPython component, set source, read
the traceback back through `gh_inspect(action='reports')`) has **not** been run yet — that needs
a Rhino session with an MCP client attached.
