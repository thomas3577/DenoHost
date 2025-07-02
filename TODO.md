# The Plan

Possibilities to run Deno in dotnet.

## API

```csharp
// Var 1
using DenoHost;

string command = "<your command>";

await Deno.Execute(command);

// Var 2
string config = "{}"; // JSON => deno.json

// public Task<object?> Execute(string command, string config)
await Deno.Execute(command, config);

// Var 3
string configFile = "<path-to-file>";

// public Task<object?> Execute(string command, string config)
await Deno.Execute(command, config);

// Var 4
IDenoConfig config = {}; // Is there a schema or a TS Interface for the deno.json

// public Task<object?> Execute(string command, IDenoConfig config)
await Deno.Execute(command, config);
```
