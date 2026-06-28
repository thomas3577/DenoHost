# DenoHost

![Build](https://github.com/thomas3577/DenoHost/actions/workflows/build.yml/badge.svg)
![Coverage](https://img.shields.io/badge/coverage-85%25-brightgreen.svg)
![NuGet](https://img.shields.io/nuget/v/DenoHost.Core.svg)
![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)

---

## About

**DenoHost** allows you to seamlessly run [Deno](https://deno.com/) scripts or
inline JavaScript/TypeScript code within your .NET applications.\
It bundles platform-specific Deno executables as separate NuGet packages and
provides a simple, consistent API for execution.

---

## Features

- Modular runtime packages (per RID)
- Clean .NET API with async execution
- Testable with xUnit
- Packaged for NuGet (multi-target)
- Linux, Windows, macOS support

---

## NuGet Packages

```bash
dotnet add package DenoHost.Core
```

| Package                        | Description                  | Platforms     |
| ------------------------------ | ---------------------------- | ------------- |
| `DenoHost.Core`                | Core execution logic (API)   | all           |
| `DenoHost.Runtime.win-x64`     | Bundled Deno for Windows     | `win-x64`     |
| `DenoHost.Runtime.win-arm64`   | Bundled Deno for Windows ARM | `win-arm64`   |
| `DenoHost.Runtime.linux-x64`   | Deno for Linux               | `linux-x64`   |
| `DenoHost.Runtime.linux-arm64` | Deno for ARM Linux           | `linux-arm64` |
| `DenoHost.Runtime.osx-x64`     | Deno for macOS Intel         | `osx-x64`     |
| `DenoHost.Runtime.osx-arm64`   | Deno for macOS Apple Silicon | `osx-arm64`   |

---

## Typed Command API

Each Deno subcommand has a dedicated method with strongly-typed options:

```csharp
using DenoHost.Core;
using DenoHost.Core.Commands;

// Run a script with permissions
await Deno.Run("app.ts", new RunOptions
{
    AllowRead = ["./data"],
    AllowNet  = [],          // empty = allow all
    Watch     = true,
});

// Evaluate TypeScript and capture JSON output
var result = await Deno.Eval<MyResult>("console.log(JSON.stringify({ ok: true }))");

// Run tests
await Deno.Test(options: new TestOptions { Filter = "my-suite" });

// Format and lint
await Deno.Fmt();
await Deno.Lint(options: new LintOptions { NoCache = true });

// Type-check
await Deno.Check(["src/main.ts"]);

// Compile to a standalone executable
await Deno.Compile("app.ts", new CompileOptions { Output = "dist/app" });

// Run a task from deno.json
await Deno.Task("build");

// Manage dependencies
await Deno.Add(["jsr:@std/fs"]);
await Deno.Remove(["jsr:@std/fs"]);
```

All methods accept an optional `DenoExecuteBaseOptions` to set the working directory or logger:

```csharp
var base = new DenoExecuteBaseOptions { WorkingDirectory = "./scripts" };
await Deno.Run("app.ts", baseOptions: base);
```

### Cancellation

Pass a `CancellationToken` as the last parameter to any command. Cancellation throws `OperationCanceledException` and terminates the underlying Deno process.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await Deno.Test(cancellationToken: cts.Token);
```

---

## Deno.Execute — Low-Level API

For cases not covered by the typed API, use `Deno.Execute` directly:

```csharp
using DenoHost.Core;

var options = new DenoExecuteBaseOptions { WorkingDirectory = "./scripts" };
string[] args = ["run", "app.ts"];

await Deno.Execute(options, args);
```

### Arguments and quoting

DenoHost passes arguments via `ProcessStartInfo.ArgumentList`. Pass each argument as its own string (avoid adding shell-style quotes inside argument strings).

```csharp
await Deno.Execute("eval", ["console.log('hello world')"]);
```

## DenoProcess Example

For long-running processes with interactive communication:

```csharp
using DenoHost.Core;

// Create a managed Deno process
using var denoProcess = new DenoProcess(
  command: "run",
  args: ["--allow-read", "server.ts"],
  workingDirectory: "./scripts"
);

// Start the process
await denoProcess.StartAsync();

// Send input to the process
await denoProcess.SendInputAsync("hello");

// Stop gracefully when done
await denoProcess.StopAsync();
```

## Requirements

- .NET 9.0+
- Deno version is bundled per RID via GitHub Releases
- No need to install Deno globally

## Security Integrity Checks

- Runtime packages verify downloaded Deno archives with SHA-256 before extraction.
- A SHA-256 checksum file is generated for the bundled executable and shipped with each runtime package.
- Runtime packages can additionally ship `deno.metadata.json` and `deno.metadata.sig`.
- `DenoHost.Core` requires signed metadata verification (signature + binary hash) before process start.
- Production deployments can enable strict mode (`DENOHOST_STRICT_MODE=true`) to block emergency bypasses.

Maintainer-only details for signing keys, release gates, alpha verification, and emergency bypass are documented in [Release Safety](./.github/release-safety.md).

## Feedback

If you're using DenoHost in a real project, I'd love to hear about it.

## License

This project is licensed under the [MIT License](./LICENSE).

## Security Policy

See [SECURITY.md](./SECURITY.md) for how to report vulnerabilities.

## Links

- [deno.com](https://deno.com/)
- [NuGet Gallery](https://www.nuget.org/packages?q=DenoHost)
