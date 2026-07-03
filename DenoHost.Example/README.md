# DenoHost.Example

An ASP.NET Core Minimal API that demonstrates how to use `DenoHost.Core` to run and manage Deno processes from .NET.

## Running

```bash
dotnet run -r win-x64
```

Then open **<http://localhost:5000>** — you will be redirected to the interactive API documentation (powered by [Scalar](https://scalar.com)).

## What's demonstrated

- **`Deno.Execute`** — fire-and-forget script execution, capturing stdout
- **`Deno.Run / Eval / Test / Bench / Fmt / Lint / Check / Compile / Task / Serve / Cache / Add / Remove`** — typed command API covering all major Deno subcommands
- **`DenoProcess`** — lower-level process control: streaming output, stdin interaction, `WaitForExitAsync`, cancellation

All endpoints are documented in Scalar with the exact `deno` command that gets executed, the working directory, and example responses.

## Scripts

The `scripts/` directory contains the TypeScript files used by the endpoints:

| File               | Used by                                  |
| ------------------ | ---------------------------------------- |
| `app.ts`           | `/run-app`                               |
| `finite-task.ts`   | `/commands/run`                          |
| `hello_test.ts`    | `/commands/test`                         |
| `hello_bench.ts`   | `/commands/bench`                        |
| `serve_handler.ts` | `/commands/serve`                        |
| `hello.ts`         | `/commands/task` (via `deno task hello`) |
