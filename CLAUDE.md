# CLAUDE.md

## Project Overview

**DenoHost** bundles platform-specific Deno executables as NuGet packages and exposes a clean .NET API (`Deno.Execute`, `DenoProcess`) for running JavaScript/TypeScript from .NET applications.

- **Single source of truth for Deno version:** `Directory.Build.props` → `<DenoVersion>`
- **Core API:** `DenoHost.Core`
- **Runtime packages (per RID):** `DenoHost.Runtime.win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`
- **CI automation scripts:** TypeScript, run with Deno (`.github/actions/**/scripts/`)

## Build & Test Commands

```bash
# Restore
dotnet restore DenoHost.sln

# Build
dotnet build DenoHost.sln

# Lint (must be clean — CI enforces this)
dotnet format DenoHost.sln --no-restore --verify-no-changes --severity warn

# Test (specify RID matching your platform)
dotnet test DenoHost.Tests/DenoHost.Tests.csproj -c Release -r win-x64

# Deno tests for CI scripts (run inside the action directory)
deno task test
```

After every code change run build and full test suite before finishing. If build or tests fail, attempt to fix the root cause; after a reasonable attempt stop and report the diagnostic output.

## Code Style

- Modern C#: file-scoped namespaces, pattern matching, primary constructors where applicable
- No unnecessary comments — only explain non-obvious intent, invariants, or workarounds
- Add or update tests for any new or modified public API, bug fix, or behavior change; skip for pure refactors, comments, or formatting
- TypeScript in CI scripts: single quotes, 2-space indent, semicolons (see `.github/actions/deno-release-check/deno.json`)

## Typed Command API — Code Generator

The typed command methods (`Deno.Run`, `Deno.Test`, etc.) and their options classes are **auto-generated**. Never edit `*.g.cs` files in `DenoHost.Core/Commands/Generated/` manually.

**When to regenerate:** After every Deno version bump (`DenoCommandsSchemaTests` fails when the snapshot is stale).

```bash
cd tools/gen-commands
deno task generate          # requires network (fetches Deno JSON schema for permissions)
deno task generate:offline  # uses built-in permission list instead
deno task test              # unit-tests the pure generator functions
```

Two sources feed the generator:

- `deno json_reference` → all CLI flags per subcommand (types inferred from `usage` patterns)
- Deno JSON schema → permission types (`read`, `write`, `net`, …) and which support `--ignore-*`

`tools/gen-commands/deno_reference.snapshot.json` is committed alongside the generated files. `DenoCommandsSchemaTests` compares it against the live binary on every test run and reports exactly which flags were added or removed.

## Architecture Notes

- **Metadata signing:** Runtime packages ship `deno.metadata.json` + `deno.metadata.sig` (ECDSA). The public key lives in `Config/metadata-signing-public.pem`. Tagged builds require the `DENOHOST_METADATA_SIGNING_PRIVATE_KEY_PEM` secret.
- **Checksum bypass:** Emergency only — requires both `DENOHOST_ALLOW_CHECKSUM_BYPASS=true` and `DENOHOST_BYPASS_REASON=...`. CI blocks publishing when bypass is active.
- **Strict mode:** `DENOHOST_STRICT_MODE=true` makes `DenoHost.Core` throw a `SecurityException` if bypass is set — use in production.

## Release Process

1. Merge Deno update PR → push `vX.Y.Z-alpha.N` tag
2. CI builds, signs, publishes alpha to NuGet, then verifies from nuget.org (`verify-alpha-from-nuget` job)
3. Push stable `vX.Y.Z` tag from the **exact same commit** as the alpha — enforced by `tag-validation` job
4. Never publish if `DENOHOST_ALLOW_CHECKSUM_BYPASS` is set

## GitHub Actions

- All Actions are pinned to commit SHAs (supply chain safety); version tag kept as a comment
- `build.yml`: full CI/CD pipeline (quality checks → build → test → smoke test → publish → GitHub Release)
- `check-deno-release.yml`: polls every 6 h for new Deno releases, opens a draft PR automatically
- `codeql.yml`: runs on push/PR to main and weekly on schedule

## Code Review Checklist

When reviewing diffs, cover these categories:

1. **.NET Efficiency:** improper async/await (blocking via `.Result`), unnecessary allocations (prefer `ReadOnlySpan<T>`, `ValueTask`), missing `IDisposable`/`IAsyncDisposable`
2. **Robustness:** `NullReferenceException` risks (respect nullable reference types), unhandled exceptions in async paths, missing input validation at system boundaries
3. **Maintainability:** high cyclomatic complexity, deeply nested logic, violations of existing architecture
4. **Testability:** hardcoded dependencies instead of DI, untestable code paths
