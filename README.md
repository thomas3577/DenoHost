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
| `DenoHost.Runtime.linux-x64`   | Deno for Linux               | `linux-x64`   |
| `DenoHost.Runtime.linux-arm64` | Deno for ARM Linux           | `linux-arm64` |
| `DenoHost.Runtime.osx-x64`     | Deno for macOS Intel         | `osx-x64`     |
| `DenoHost.Runtime.osx-arm64`   | Deno for macOS Apple Silicon | `osx-arm64`   |

---

## Deno.Execute Example

For simple script execution with immediate results:

```csharp
using DenoHost.Core;

var options = new DenoExecuteBaseOptions { WorkingDirectory = "./scripts" };
string[] args = ["run", "app.ts"];

await Deno.Execute(options, args);
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

## Feedback

If you're using DenoHost in a real project, I'd love to hear about it.

## License

This project is licensed under the [MIT License](./LICENSE).

## Security Policy

See [SECURITY.md](./SECURITY.md) for how to report vulnerabilities.

## Links

- [deno.com](https://deno.com/)
- [NuGet Gallery](https://www.nuget.org/packages?q=DenoHost)
