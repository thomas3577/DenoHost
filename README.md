# DenoWrapper

![Build & Publish](https://github.com/<USERNAME>/<REPO>/actions/workflows/build.yml/badge.svg)

> 🦕 Use Deno from within your .NET applications – cross-platform, cleanly
> packaged, and NuGet-ready.

---

## 💡 About

**DenoWrapper** allows you to seamlessly run [Deno](https://deno.com/) scripts
or inline JavaScript/TypeScript code within your .NET applications.\
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

| Package                           | Description                  | Platforms     |
| --------------------------------- | ---------------------------- | ------------- |
| `DenoWrapper.Core`                | Core execution logic (API)   | all           |
| `DenoWrapper.Runtime.win-x64`     | Bundled Deno for Windows     | `win-x64`     |
| `DenoWrapper.Runtime.linux-x64`   | Deno for Linux               | `linux-x64`   |
| `DenoWrapper.Runtime.linux-arm64` | Deno for ARM Linux           | `linux-arm64` |
| `DenoWrapper.Runtime.osx-x64`     | Deno for macOS Intel         | `osx-x64`     |
| `DenoWrapper.Runtime.osx-arm64`   | Deno for macOS Apple Silicon | `osx-arm64`   |

---

## 🚀 Usage Example

```csharp
using DenoWrapper;

await Deno.Execute("console.log('Hello from Deno');");
```

Or with result:

```csharp
string? output = await Deno.Execute<string>("console.log('42');");
Console.WriteLine(output); // "42"
```

## 🛠️ Requirements

- .NET 9.0+
- Deno version is bundled per RID via GitHub Releases
- No need to install Deno globally

## 📄 License

MIT License © Thomas Huber

## 🌐 Links

- [deno.com](https://deno.com/)
- [NuGet Gallery](https://www.nuget.org/packages?q=denowrapper)
