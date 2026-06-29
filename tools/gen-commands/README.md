# gen-commands

Code generator for the DenoHost typed command API.

Reads `deno json_reference` (the live Deno binary) and the Deno JSON schema to produce:

| Output file                                                  | Description                                                                         |
| ------------------------------------------------------------ | ----------------------------------------------------------------------------------- |
| `DenoHost.Core/Commands/Generated/XxxOptions.g.cs`           | One options class per subcommand (`RunOptions`, `ServeOptions`, …)                  |
| `DenoHost.Core/Commands/Generated/Deno.Commands.g.cs`        | `Deno.Run(…)`, `Deno.Serve(…)`, … factory methods                                   |
| `DenoHost.Core/Commands/Generated/DenoProcess.Commands.g.cs` | `DenoProcess.Run(…)`, `DenoProcess.Serve(…)`, `DenoProcess.Task(…)` factory methods |
| `deno_reference.snapshot.json`                               | Flag snapshot used by `DenoCommandsSchemaTests` to detect drift                     |

Never edit `*.g.cs` files by hand — they are overwritten on the next `generate` run.

---

## Usage

Run from this directory:

```bash
# Requires network (fetches Deno JSON schema for permission types)
deno task generate

# Offline fallback — uses a hardcoded permission list instead
deno task generate:offline

# Unit-test the pure generator functions
deno task test
```

After generating, run the .NET build to confirm the output compiles:

```bash
dotnet build DenoHost.sln
```

---

## When to regenerate

Regenerate whenever the installed Deno binary changes — i.e. after every `<DenoVersion>` bump in `Directory.Build.props`.

The test `DenoCommandsSchemaTests.GeneratedOptions_MatchCurrentDenoJsonReference` compares `deno_reference.snapshot.json` against the live binary on every `dotnet test` run and reports exactly which flags were added or removed. When that test fails, run `deno task generate` and commit the result.

---

## Release workflow

CI does **not** run the generator automatically. New Deno releases follow this sequence:

1. **Automated PR** — `check-deno-release.yml` runs every 6 hours, detects a new Deno version, and opens a draft PR that bumps `<DenoVersion>` in `Directory.Build.props`.

2. **Check for schema drift** — `DenoCommandsSchemaTests` will fail on the PR if new or removed flags exist. Inspect the diff to decide whether to add/remove properties in the generated code.

3. **Regenerate if needed** — inside the release branch:

   ```bash
   cd tools/gen-commands
   deno task generate     # or generate:offline if no network
   ```

   Commit the updated `*.g.cs` files and `deno_reference.snapshot.json`.

4. **Merge** the PR into `main`.

5. **Alpha tag** — on the merge commit:

   ```bash
   git tag vX.Y.Z-alpha.N && git push --tags
   ```

   CI builds, signs, publishes to NuGet, and verifies the packages from nuget.org (`verify-alpha-from-nuget` job).

6. **Stable tag** — from the **exact same commit**:

   ```bash
   git tag vX.Y.Z && git push --tags
   ```

   CI enforces that the stable tag shares a commit with an existing `alpha.N` tag (`tag-validation` job).

---

## How it works

Two sources feed the generator:

- **`deno json_reference`** — emits a JSON document with every subcommand and flag. Flag types are inferred from the `usage` pattern (e.g. `<PATH>...` → `string[]?`, `<NUMBER>` → `int?`, bare flag → `bool?`).
- **Deno JSON schema** (fetched from GitHub) — provides the canonical list of permission types (`read`, `write`, `net`, …) and which ones support `--ignore-*`.

The generator is configured via two arrays at the top of `generate.ts`:

- `COMMANDS` — all subcommands to generate options classes and `Deno.*` methods for.
- `PROCESS_COMMAND_NAMES` — subset of `COMMANDS` that also get `DenoProcess.*` factory methods (long-running commands only: `run`, `serve`, `task`).

Flags in `SKIP_FLAGS` are never emitted (e.g. `--config`, `--help`, `--inspect`).
