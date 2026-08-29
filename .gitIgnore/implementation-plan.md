# Sherlock MCP Implementation Plan

## Context: .NET Version Coverage & First-Class MCP Improvements (August 2026)

### Executive Summary

Sherlock MCP is well-positioned on the MCP 2.1.0 stack (spec 2026-07-28). Rather than dropping EOL frameworks (net8.0, net9.0), this plan makes framework targeting configurable via CLI flags. This keeps Sherlock valuable for the exact scenario it's designed for: helping users understand and migrate legacy .NET codebases to modern frameworks. The plan also addresses MCP feature gaps (Resources, Prompts, caching hints) that unlock enterprise-grade deployment and production scalability.

---

## Phase 1: Configurable .NET Version Targeting (Priority: 🔴 High | Effort: 1-2 hrs)

### Phase 1 Rationale

Rather than dropping net8.0/net9.0 (EOL Nov 10, 2026), make framework targeting configurable. Sherlock is uniquely positioned to help users *analyze and migrate* legacy projects to modern frameworks. Supporting EOL frameworks keeps the tool useful for the exact scenario it's designed for: understanding what needs to change in old codebases.

### Current State

- **Targeting:** net8.0, net9.0, net10.0
- **Status:** net8.0 and net9.0 both reached EOL on November 10, 2026
- **net10.0:** Current LTS, supported through November 2028
- **net11.0:** Latest STS (Standard Term Support, 2 years)

### Design: `--target-frameworks` CLI Option

Add a configuration flag to let users select which frameworks to target:

```bash
# Default (modern stack)
dotnet run --project src/server/Sherlock.MCP.Server.csproj

# Include legacy frameworks for analysis
dotnet run --project src/server/Sherlock.MCP.Server.csproj --target-frameworks net6.0,net7.0,net8.0,net9.0,net10.0,net11.0

# Minimal (LTS only)
dotnet run --project src/server/Sherlock.MCP.Server.csproj --target-frameworks net10.0
```

### Implementation Steps

#### 1.1 Update `Directory.Build.props`

Keep all frameworks for build capacity:

```xml
<TargetFrameworks>net6.0;net7.0;net8.0;net9.0;net10.0;net11.0</TargetFrameworks>
```

#### 1.2 Add Configuration to Server

**File:** `src/server/Program.cs`

```csharp
var builder = Host.CreateEmptyApplicationBuilder(args);

// New: Parse CLI option for target frameworks
var targetFrameworks = args
    .SkipWhile(a => a != "--target-frameworks")
    .Skip(1)
    .FirstOrDefault()?
    .Split(',')
    .Select(tf => tf.Trim())
    .ToArray()
    ?? new[] { "net10.0", "net11.0" }; // Default: modern stack

// Wire into services
builder.Services.AddSingleton(new FrameworkOptions { TargetFrameworks = targetFrameworks });
```

**File:** `src/runtime/Configuration/FrameworkOptions.cs` (new)

```csharp
namespace Sherlock.MCP.Runtime.Configuration;

public class FrameworkOptions
{
    public string[] TargetFrameworks { get; set; } = Array.Empty<string>();
    
    public bool IsFrameworkSupported(string framework) => 
        TargetFrameworks.Contains(framework, StringComparer.OrdinalIgnoreCase);
}
```

#### 1.3 Expose in MCP Instructions

When Sherlock initializes, advertise supported frameworks:

**File:** `src/server/Handlers/ServerHandler.cs`

```csharp
var supportedFrameworks = frameworkOptions.TargetFrameworks;
instructions += $"\nThis instance targets: {string.Join(", ", supportedFrameworks)}";
```

#### 1.4 Document in Usage

**README.md** addition:

```markdown
### Running Against Specific .NET Versions

By default, Sherlock targets net10.0 and net11.0 (modern stack). To analyze legacy projects 
or support framework migration workflows, pass `--target-frameworks`:

```bash
# Modern stack (default)
dotnet run --project src/server/Sherlock.MCP.Server.csproj

# Legacy analysis (help modernize old codebases)
dotnet run --project src/server/Sherlock.MCP.Server.csproj \
  --target-frameworks net6.0,net7.0,net8.0,net9.0,net10.0,net11.0

# LTS only
dotnet run --project src/server/Sherlock.MCP.Server.csproj \
  --target-frameworks net10.0
```text

This is especially useful for agents tasked with framework migration analysis — they can 
introspect multiple framework versions of the same assembly and recommend upgrade paths.
```

### Use Case: Framework Migration Analysis

With configurable targeting, an agent can:

1. Load the same assembly compiled for net8.0, net9.0, and net10.0
2. Compare type/method signatures across versions
3. Identify breaking changes and migration blockers
4. Suggest modernization path (e.g., "net8 → net10 has these dependency changes")

Example agent prompt:

```text
Analyze MyLib compiled for net8.0 and net10.0. 
Using Sherlock, compare their APIs. What changed?
What migration path would you recommend?
```

### CI/CD Implications

**No change to default CI behavior:**

- `dotnet build src/Sherlock.MCP.slnx` still targets all frameworks (good for library compatibility testing)
- Release builds default to net10.0/net11.0 (modern stack)
- Users can override via CLI or environment variable if needed

### Test Plan

- [ ] `--target-frameworks net10.0,net11.0` defaults to modern stack
- [ ] `--target-frameworks net6.0,net7.0,net8.0,net9.0,net10.0,net11.0` includes legacy
- [ ] `--target-frameworks invalid` fails gracefully with helpful error
- [ ] `FrameworkOptions.IsFrameworkSupported()` correctly filters
- [ ] MCP `instructions` field correctly advertises configured frameworks
- [ ] Help text documents the option (`--help` or similar)

### Future Cadence

- **v2.14.0:** Introduce `--target-frameworks` flag
- **v2.15.0 (Nov 2026):** Add net12.0 to available choices
- **v3.0.0 (Nov 2027):** Consider dropping net6.0/net7.0 if no demand; keep newer versions for migration scenarios

---

## Phase 2: Resources Implementation (Priority: 🟠 High | Effort: 2-3 hrs)

### Phase 2 Rationale

MCP defines three primitives: **Tools** (actions), **Resources** (context data), **Prompts** (templates). Sherlock excels at Tools but is missing Resources and Prompts. Resources enable clients to query static assembly metadata as embedded context, reducing token consumption vs. calling tools.

### Design

#### 2.1 Resource URIs

Define a hierarchy of queryable assembly metadata:

```text
assembly://path/to/MyLib.dll/types
  → List all public types with summary

assembly://path/to/MyLib.dll/types/MyNamespace.MyType
  → Type metadata (hierarchy, interfaces, nested types)

assembly://path/to/MyLib.dll/types/MyNamespace.MyType/members
  → Methods, properties, fields, events with signatures

assembly://path/to/MyLib.dll/references
  → Resolved assembly dependencies

assembly://path/to/MyLib.dll/metadata
  → Assembly-level: identity, version, target framework, attributes
```

#### 2.2 Resource Contract

Each resource returns JSON with metadata and optional markdown summary:

```json
{
  "uri": "assembly://path/to/MyLib.dll/types",
  "name": "Types in MyLib.dll",
  "description": "Public types with brief metadata",
  "mimeType": "text/plain",
  "contents": "MyNamespace.MyType (class)\n  Base: object\n  Implements: IDisposable\n..."
}
```

#### 2.3 Implementation Steps

1. **Create `IResourceProvider` interface** (`src/runtime/Resources/`)
   - `GetResource(uri: string, args?: dict) → Resource`
   - `ListResources() → ResourceDescription[]`

2. **Implement resource handlers** per URI pattern
   - `TypesResourceHandler` - lists types or drills into a type
   - `MembersResourceHandler` - lists members for a type
   - `MetadataResourceHandler` - assembly-level info
   - `ReferencesResourceHandler` - dependency chain

3. **Wire into MCP server** (`src/server/Handlers/ResourcesHandler.cs`)
   - Handle `resources/list` request (advertise all patterns)
   - Handle `resources/read` request (URI dispatch + content generation)
   - Cache resource content using existing `IToolResponseCache`

4. **Update `server.json` registry entry**
   - Add `resourceTemplates` with URI patterns

5. **Document in `README.md`**
   - Example: "Query types as context without calling `GetTypesFromAssembly`"

### Phase 2 Example Usage (from agent perspective)

```shell
Client: resources/list
Server: 
[
  { "uri": "assembly://...", "name": "Types", "mimeType": "text/plain" },
  { "uri": "assembly://...", "name": "Metadata", "mimeType": "application/json" }
]

Client: resources/read?uri=assembly:///path/MyLib.dll/types
Server:
{
  "contents": "Namespace.Class1\n  Methods: Foo(), Bar(string x)\n..."
}
```

### Success Criteria

- All resource handlers have unit tests
- Resource content is cached and obeys TTL policies
- `server.json` registry validates
- README includes Resource section with examples
- E2E test in integration-tests calls `resources/list` and `resources/read`

---

## Phase 3: Prompts Implementation (Priority: 🟠 High | Effort: 1-2 hrs)

### Phase 3 Rationale

MCP Prompts are reusable, parameterized message templates. Sherlock ships guidance in `instructions` but formalizing common analysis patterns as Prompts gives agents discoverable workflows without reading docs.

### Phase 3 Design

#### 3.1 Prompt Definitions

```json
{
  "name": "api-surface-analysis",
  "description": "Analyze public API surface of an assembly",
  "arguments": [
    { "name": "assemblyPath", "description": "Path to .dll", "required": true }
  ]
}
```

#### 3.2 Built-in Prompts

| Name | Args | Description |
| ------ | ------ | ------------- |
| `api-surface-analysis` | `assemblyPath` | List all public types, methods, properties (use for API review) |
| `type-hierarchy-trace` | `assemblyPath`, `typeName` | Full inheritance chain + implementations (use for understanding design) |
| `method-call-graph` | `assemblyPath`, `methodName`, `typeName` | What this method calls + what calls it (use for tracing logic) |
| `dependency-inventory` | `assemblyPath` | All referenced assemblies + versions (use for impact analysis) |
| `breaking-change-detection` | `oldAssemblyPath`, `newAssemblyPath` | Compare signatures for removed/changed members (use for versioning) |

#### 3.3 Implementation Steps

1. **Create `IPromptProvider` interface** (`src/runtime/Prompts/`)
   - `GetPrompt(name: string) → PromptDefinition`
   - `ListPrompts() → PromptDefinition[]`
   - `RenderPrompt(name: string, args: dict) → string` (returns expanded message for agent context)

2. **Implement prompt library** (`src/runtime/Prompts/PromptLibrary.cs`)
   - Load prompt definitions from JSON or code
   - Validate arguments at render time
   - Return rendered messages with tool call hints

3. **Wire into MCP server** (`src/server/Handlers/PromptsHandler.cs`)
   - Handle `prompts/list` request
   - Handle `prompts/get` request (return definition + rendered message)
   - Caching: prompts are deterministic, cache for 1 hour

4. **Document in `README.md`**
   - Add Prompts section with example workflows

### Example Usage (from agent perspective)

```shell
Client: prompts/list
Server:
[
  { "name": "api-surface-analysis", "description": "..." },
  { "name": "type-hierarchy-trace", "description": "..." },
  ...
]

Client: prompts/get?name=api-surface-analysis&arguments={assemblyPath: "/path/MyLib.dll"}
Server:
{
  "messages": [
    {
      "role": "user",
      "content": "Analyze the API surface of MyLib.dll. Use get_types_from_assembly to list all public types, then get_type_methods for each to summarize the public surface."
    }
  ]
}
```

### Phase 3 Success Criteria

- All 5 prompts have clear descriptions and parameter schemas
- Prompts are unit-tested with various argument combinations
- Integration test calls `prompts/list` and `prompts/get`
- README includes Prompts section with workflow examples
- MCP client can discover and invoke prompts

---

## Phase 4: Response Caching Hints (Priority: 🟡 Medium | Effort: 1 hr)

### Phase 4 Rationale

MCP supports `cacheHint` in tool responses. Sherlock advertises `ttlMs` on `tools/list` but individual tool responses lack caching metadata. This helps clients cache aggressively.

### Changes

**File:** `src/server/Shared/JsonHelpers.cs` and tool handlers

#### 4.1 Add `cacheHint` to Response Envelope

```csharp
// BEFORE
public static string Envelope(string kind, object data) =>
    JsonSerializer.Serialize(new { kind, version = "1.0.0", data });

// AFTER
public static string Envelope(
    string kind, 
    object data, 
    (int ttlMs, string cacheScope)? cacheHint = null) =>
    JsonSerializer.Serialize(new 
    { 
        kind, 
        version = "1.0.0", 
        data,
        cacheHint = cacheHint == null ? null : new 
        {
            ttlMs = cacheHint.Value.ttlMs,
            cacheScope = cacheHint.Value.cacheScope
        }
    });
```

#### 4.2 Apply to High-Value Tools

- `GetTypeInfo`, `GetTypeMethods`, `GetTypeProperties`: 1 hour TTL, "public" scope (assembly contents don't change)
- `GetAssemblyInfo`: 24 hour TTL, "public" scope
- `AnalyzeSolution`, `AnalyzeProject`: 1 hour TTL, "public" scope
- `UpdateRuntimeOptions`: no caching (it's mutating)
- `FindAssemblyByNugetPackage`: 24 hour TTL (packages are immutable)

#### 4.3 Test

- Unit test verifies caching hints are correctly serialized
- Integration test verifies clients receive the hints

### Phase 4 Success Criteria

- All read-only tools advertise appropriate `cacheHint`
- `UpdateRuntimeOptions` omits `cacheHint`
- README documents which tools are cacheable and for how long

---

## Phase 5: EMA Extension Support (Priority: 🟡 Medium | Effort: 4-6 hrs)

### Phase 5 Rationale

Enterprise-Managed Authorization (EMA) is an MCP extension (now stable) enabling SSO/RBAC gating. Sherlock currently has no auth layer. For enterprise deployments in shared environments (e.g., orchestrated LLM services), EMA is required.

### Design Sketch (not full implementation)

#### 5.1 EMA Integration Points

- **Server initialization:** Accept optional EMA broker endpoint and client credentials
- **Tool dispatch:** Before executing a tool, check authorization with EMA broker
- **Error handling:** Return structured auth failures (not generic errors)

#### 5.2 Implementation (Deferred)

1. Create `IAuthorizationProvider` interface
2. Implement `EmaAuthorizationProvider` for EMA brokers
3. Inject into tool handlers via DI
4. Wrap tool execution with auth check
5. Document in README under "Enterprise Deployment"

#### 5.3 Configuration

```json
{
  "servers": {
    "sherlock": {
      "command": "sherlock-mcp",
      "env": {
        "EMA_BROKER_URL": "https://auth.company.com/mcp-ema",
        "EMA_CLIENT_ID": "sherlock-instance-1"
      }
    }
  }
}
```

### Phase 5 Notes

This is scoped as **deferred** (post-v2.14.0) pending demand signal. If enterprises request it, bump to Phase 2.

---

## Phase 6: .well-known Metadata (Priority: 🟢 Low | Effort: 1-2 hrs)

### Phase 6 Rationale

MCP 2026-07-28 introduces `.well-known/mcp.json` for static capability discovery. Registries and gateways can learn what Sherlock does without connecting.

### Phase 6 Design

#### 6.1 Endpoint: `GET /.well-known/mcp.json`

```json
{
  "name": "Sherlock MCP",
  "version": "2.14.0",
  "description": "Deep .NET assembly introspection via MCP",
  "tools": 36,
  "supportedFrameworks": ["net10.0", "net11.0"],
  "capabilities": {
    "tools": true,
    "resources": true,
    "prompts": true,
    "ema": false
  },
  "transport": ["stdio"],
  "repositoryUrl": "https://github.com/jcucci/dotnet-sherlock-mcp",
  "registryEntry": "io.github.jcucci/dotnet-sherlock-mcp"
}
```

#### 6.2 Implementation

- Serve from a static endpoint (only via Streamable HTTP transport if used)
- For stdio mode, add as a pseudo-tool `self/metadata` or document in MCP registry JSON

### Phase 6 Notes

This is **low priority** for CLI/stdio servers (Sherlock's primary mode) but becomes valuable if Sherlock supports Streamable HTTP in the future.

---

## Implementation Roadmap

### v2.14.0 (Q3 2026 Target)

- ✅ Phase 1: Configurable .NET framework targeting (net6.0-net11.0 available, net10.0/net11.0 default)
- ✅ Phase 2: Resources (assembly metadata queries)
- ✅ Phase 3: Prompts (workflow templates)
- ✅ Phase 4: Response caching hints

### v2.15.0 (Q4 2026 Target)

- ⏳ Phase 5: EMA extension (if demand signals)
- ⏳ Phase 6: .well-known metadata (if Streamable HTTP support planned)

### Future Versions

- Event-driven resource updates (MCP roadmap "On the Horizon")
- Structured error recovery hints in all errors
- Streaming responses for large result sets
- Net12.0 and beyond (add to available choices annually)

---

## Testing Checklist

### Unit Tests

- [ ] .NET 11 build succeeds
- [ ] Resource handlers return valid JSON
- [ ] Prompts render with correct parameter interpolation
- [ ] Cache hints are included in response envelopes
- [ ] Auth provider (if implemented) rejects unauthorized calls

### Integration Tests

- [ ] `resources/list` returns all resource patterns
- [ ] `resources/read` returns content for each pattern
- [ ] `prompts/list` returns all 5 prompts
- [ ] `prompts/get` renders prompts with arguments
- [ ] `tools/list` includes caching hints
- [ ] Existing tool responses include caching hints

### E2E Tests (Manual)

- [ ] Cursor/Claude Desktop can discover and call Resources
- [ ] Cursor/Claude Desktop can discover and invoke Prompts
- [ ] Prompt-based workflows execute successfully
- [ ] No regression in existing tool behavior

---

## Documentation Updates

### README.md

- [ ] Update target frameworks to net10.0, net11.0
- [ ] Add "Resources" section with URI patterns and examples
- [ ] Add "Prompts" section with workflow examples
- [ ] Add "Caching" section explaining TTL and cache scope
- [ ] Add "Enterprise" section (placeholder for EMA, if implemented)

### CLAUDE.md / AGENTS.md

- [ ] Update guidance to recommend Resources for context queries
- [ ] Add examples of prompt discovery and invocation

### server.json (MCP Registry)

- [ ] Add `resourceTemplates` with URI patterns
- [ ] Update `capabilities` object if registry schema supports it

---

## Misc Notes

- **Breaking changes:** None. By keeping all frameworks in the build matrix and making targeting configurable, v2.14.0 remains backward compatible. Users on net8.0/net9.0 can still use Sherlock by specifying `--target-frameworks` at runtime.
- **Framework migration as a feature:** Sherlock becomes a tool for *understanding* framework upgrades, not just consuming modern frameworks. This is a unique position in the .NET tooling ecosystem.
- **Backward compatibility:** Resources and Prompts are new MCP primitives; clients that don't support them simply won't call them (MCP design).
- **Performance:** All phases maintain current latency. Resources are cached like tool responses.
- **Token efficiency:** Resources reduce redundant tool calls by ~30% in typical analysis workflows (empirical estimate pending).

---

## Decision Points

- **Framework coverage:** Targeting net6.0–net11.0 ensures Sherlock can analyze projects across a wide modernization window (6.0 → 11.0 is 5+ years of upgrades). Default to net10.0/net11.0 but let users opt into legacy analysis.
- **EMA deferral:** Waiting for enterprise demand signal before investing 4-6 hrs
- **.well-known timing:** Defer until (1) Streamable HTTP support is planned, or (2) registry integration requires it
- **Prompt triggers:** Consider SEP-1686 (Tasks) integration if Sherlock needs to support long-running assembly analyses
