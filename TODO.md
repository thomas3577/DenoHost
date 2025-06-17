# The Plan

Possibility to run Deno in dotnet.

Don't forget crossplattform!

## API

```csharp
// Var 1
using DenoWrapper;

string command = "<your command>";

await Deno.Execute(command);

// Var 2
string config = "{}"; // JSON

// public Task<object?> Execute(string command, string config)
await Deno.Execute(command, config);

// Var 3
string configFile = "<path-to-file>";

// public Task<object?> Execute(string command, string config)
await Deno.Execute(command, config);

// Var 4
IDenoConfig config = {};

// public Task<object?> Execute(string command, IDenoConfig config)
await Deno.Execute(command, config);
```
