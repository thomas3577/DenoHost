# DenoHost

![Build](https://github.com/thomas3577/DenoHost/actions/workflows/build.yml/badge.svg)
![NuGet](https://img.shields.io/nuget/v/DenoHost.Core.svg)
![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)

> ⚠️ **EXPERIMENTAL ALPHA** ⚠️\
> This library is in active development. APIs and features may change
> significantly between releases.\
> Not recommended for production use. Use for testing and experimentation only.

> 🦕 Use Deno from within your .NET applications – cross-platform, cleanly
> packaged, and NuGet-ready.

---

## 💡 About

**DenoHost** allows you to seamlessly run [Deno](https://deno.com/) scripts or
inline JavaScript/TypeScript code within your .NET applications.\
It bundles platform-specific Deno executables as separate NuGet packages and
provides a simple, consistent API for execution.

---

## ✨ Features

- 🧩 Modular runtime packages (per RID)
- 🛠️ Clean .NET API with async execution
- 🧪 Testable with xUnit
- 📦 Packaged for NuGet (multi-target)
- 🐧 Linux, 🪟 Windows, 🍎 macOS support

---

## 📦 NuGet Packages

Die Packages sind sowohl über NuGet.org als auch über GitHub Packages verfügbar:

### NuGet.org

```bash
dotnet add package DenoHost.Core
```

### GitHub Packages

```bash
# Zunächst GitHub Packages als Quelle hinzufügen
dotnet nuget add source --username USERNAME --password GITHUB_TOKEN --store-password-in-clear-text --name github "https://nuget.pkg.github.com/thomas3577/index.json"

# Dann Package installieren
dotnet add package DenoHost.Core --source github
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

## 🚀 Usage Example

```csharp
using DenoHost;

var options = new DenoExecuteBaseOptions { WorkingDirectory = "./scripts" };
string[] args = ["run", "app.ts"];

await Deno.Execute(options, args);
```

## 🛠️ Requirements

- .NET 9.0+
- Deno version is bundled per RID via GitHub Releases
- No need to install Deno globally

## 📄 License

MIT License © Thomas Huber

## 🌐 Links

- [deno.com](https://deno.com/)
- [NuGet Gallery](https://www.nuget.org/packages?q=DenoHost)
- [GitHub Packages](https://github.com/thomas3577/deno-dotnet/packages)
