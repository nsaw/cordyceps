# Cordyceps — Rhino 7 fork

> **This is a fork.** Upstream is [brookstalley/cordyceps](https://github.com/brookstalley/cordyceps),
> which targets **Rhino 8 / .NET 8** and is the version you want unless you are specifically stuck
> on Rhino 7. This fork retargets to **Rhino 7 / .NET Framework 4.8** and is deliberately scoped to
> the Grasshopper tools only — the Rhino-side scene and render tools are removed. See
> [RHINO7-SPIKE.md](RHINO7-SPIKE.md) for what changed and why, and
> [upstream issue #28](https://github.com/brookstalley/cordyceps/issues/28) for the net48 discussion.
>
> Do **not** install the upstream `.gha` against Rhino 7, and do not install this one against
> Rhino 8. They are built for different runtimes.

**MCP server for Grasshopper.** Give AI agents or scripts direct control over your parametric design canvas.

[Model Context Protocol (MCP)](https://modelcontextprotocol.io/) provides a standardized interface for controlling applications — whether from AI assistants or your own code.

## Features

- **Full Grasshopper control** — add components, wire connections, set values, manage groups
- **Bake to Rhino**: bake Grasshopper geometry into the Rhino document (`gh_canvas` `bake`); the Rhino scene and render tools are removed in this fork
- **Natural language** — describe what you want and let AI build it
- **Direct scripting** — call tools from Python or any MCP client, no AI required

## Requirements

- **Rhino 7** (.NET Framework 4.8 on Windows, Mono on macOS). Verified against 7.38.24338.17002.
- **For AI use**: Claude Desktop, Claude Code, Cursor, VS Code, or any MCP-compatible assistant
- **For scripting**: Any MCP client library ([Python](https://github.com/modelcontextprotocol/python-sdk), [TypeScript](https://github.com/modelcontextprotocol/typescript-sdk), etc.)

## Installation

This fork is **not** published to the Rhino Package Manager (Yak). Build it, or use the `.gha`
committed to `releases/` on this branch. The Package Manager entry named "Cordyceps" is upstream's
Rhino 8 build and will not load in Rhino 7.

### Manual install

1. Build it (see [Building](#building)), or take `releases/Cordyceps.gha` from this branch

2. Copy to your Grasshopper components folder:
   *File → Special Folders → Components Folder*

3. Unblock the file so Rhino will load it:
   - **Windows**: right-click → Properties → check "Unblock" → OK
   - **macOS**: clear the quarantine flag (e.g. `xattr -dr com.apple.quarantine <path-to-Cordyceps.gha>`)

4. Restart Rhino

## Usage

1. Drop the **Cordyceps** component on your canvas (*Params → Util → Cordyceps*)

   The server starts on port 26929 by default. Optional inputs:
   - **HttpPort**: Change the HTTP port
   - **DebugLevel**: Set to 1+ to see request/response traffic in Rhino

2. Configure your MCP client:

   <details>
   <summary><strong>Claude Desktop</strong></summary>

   Claude Desktop uses stdio transport, so it needs the `mcp-remote` bridge. Requires [Node.js](https://nodejs.org/).

   Add to your config file:
   - macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
   - Windows: `%APPDATA%\Claude\claude_desktop_config.json`

   ```json
   {
     "mcpServers": {
       "cordyceps": {
         "command": "npx",
         "args": ["-y", "mcp-remote", "http://127.0.0.1:26929/mcp"]
       }
     }
   }
   ```

   Restart Claude Desktop after saving.
   </details>

   <details>
   <summary><strong>Claude Code</strong></summary>

   ```
   claude mcp add --transport http cordyceps http://127.0.0.1:26929/mcp
   ```
   </details>

   <details>
   <summary><strong>Cursor, VS Code, and other HTTP clients</strong></summary>

   ```json
   {
     "mcpServers": {
       "cordyceps": {
         "type": "streamable-http",
         "url": "http://127.0.0.1:26929/mcp"
       }
     }
   }
   ```
   </details>

3. Start building — describe what you want in natural language

## Example

```
Make an animated GIF that shows the full journey from parametric modeling
to photorealistic render. The subject is a small collection of geometric
forms — maybe five or six objects with varied shapes, scales, and
proportions. Arrange them as a pleasing composition.
```

![Cordyceps Showcase](images/cordyceps_showcase.gif)

The AI builds everything autonomously — creating parametric geometry in Grasshopper, baking to Rhino, applying PBR materials, configuring the render environment and lighting, and capturing a smooth orbiting animation.

## Scripting

Call tools directly from Python or any MCP client library:

```python
from mcp import ClientSession

async with ClientSession(transport) as session:
    # Add a slider and circle
    slider = await session.call_tool('gh_canvas', {
        'action': 'add', 'type': 'Number Slider', 'x': 50, 'y': 50
    })
    circle = await session.call_tool('gh_canvas', {
        'action': 'add', 'type': 'Curve/Circle', 'x': 200, 'y': 50
    })

    # Connect slider output to circle radius
    await session.call_tool('gh_wire', {
        'action': 'connect',
        'sourceId': slider['id'], 'sourceParam': '0',
        'targetId': circle['id'], 'targetParam': 'R'
    })
```

See the [MCP Python SDK](https://github.com/modelcontextprotocol/python-sdk) for transport setup and client details.

## Tools

This fork provides **5 tools** — the Grasshopper set only. Related operations are grouped under a
single tool with an `action` parameter to minimise context window usage. Upstream additionally ships
`rhino_scene` and `rhino_render`; both are removed here (see RHINO7-SPIKE.md for the rationale).

### Grasshopper

| Tool | Description |
|------|-------------|
| `gh_canvas` | Components, values, groups, baking, variable parameters |
| `gh_wire` | Connection management |
| `gh_document` | Save, clear, snapshots (max 20 kept, oldest evicted), solver control, canvas capture (undo/redo are disabled — use snapshots) |
| `gh_script` | Script component configuration |
| `gh_inspect` | Status, outputs, data tracing, debugging |

### Rhino

Removed in this fork. `rhino_scene` and `rhino_render` were the largest and most Rhino-8-coupled
part of the codebase (~2,830 LOC, heavy `RenderContent`/PBR use) and were not needed for the
GhPython workflow this fork exists to serve. Use upstream on Rhino 8 if you want them.

## Documentation

Cordyceps exposes guides and patterns to MCP clients as resources. Your AI assistant can read these automatically when it needs guidance on data trees, type systems, component patterns, or rendering workflows.

Browse the documentation directly: [`src/Cordyceps/Knowledge/`](src/Cordyceps/Knowledge/)

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Plugin won't load | Verify you are on Rhino 7 and that this is the Rhino 7 build, not upstream's. Unblock the .gha (Windows) or clear its quarantine flag (macOS): `xattr -dr com.apple.quarantine <path>`. |
| Script component not found | This fork resolves `python` / `ghpython` to Rhino 7's **GhPython Script** component (`Math` > `Script`, reported category `Maths`). Rhino 8's "Python 3 Script" does not exist here. |
| Can't connect | Ensure Cordyceps component is on canvas. Check the port. |
| Claude Desktop can't connect | Ensure Node.js is installed. Check Rhino is running with Cordyceps. Restart Claude Desktop after config changes. |
| Component not found | Use `gh_canvas(action='search', query='...')` to find exact names. |
| No command output | Set DebugLevel input to 1 to see traffic in Rhino command history. |

## Building

```bash
dotnet build src/Cordyceps/Cordyceps.csproj -c Release
```

Only Release builds are supported — a Debug build fails with an explicit error.

No .NET Framework Developer Pack is required, on any platform: the net48 reference assemblies come
from the `Microsoft.NETFramework.ReferenceAssemblies.net48` NuGet package. The output cannot *run*
under the .NET SDK on macOS, which is fine — Rhino 7's own Mono runtime executes it.

---

[Changelog](CHANGELOG.md) · [Rhino 7 fork notes](RHINO7-SPIKE.md) · Upstream: [brookstalley/cordyceps](https://github.com/brookstalley/cordyceps) by Brooks Talley · Inspired by [grasshopper-mcp](https://github.com/alfredatnycu/grasshopper-mcp) by Alfred Chen · MIT License
