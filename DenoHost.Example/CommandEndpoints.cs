using DenoHost.Core;
using DenoHost.Core.Commands;

namespace DenoHost.Example;

public static class CommandEndpoints
{
  public static WebApplication MapCommandEndpoints(this WebApplication app)
  {
    var scriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts");
    var baseOpts = new DenoExecuteBaseOptions { WorkingDirectory = scriptsPath };

    app.MapGet("/commands/run", async () =>
    {
      var output = await Deno.Run<string>("finite-task.ts",
        options: new RunOptions { AllowRead = [] },
        baseOptions: baseOpts);
      return Results.Text(output);
    })
    .WithName("CommandRun")
    .WithSummary("deno run")
    .WithDescription("""
      Runs a TypeScript script to completion and returns its stdout output.

      **Command**
      ```
      deno run --allow-read finite-task.ts
      ```

      **Working directory:** `scripts/`

      `AllowRead = []` grants read access to all paths (equivalent to `--allow-read` without a path restriction).
      The script exits on its own — no long-running process.
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/eval", async () =>
    {
      var output = await Deno.Eval<string>(
        """console.log(JSON.stringify({ hello: "world", time: new Date().toISOString() }))""");
      return Results.Text(output);
    })
    .WithName("CommandEval")
    .WithSummary("deno eval")
    .WithDescription("""
      Evaluates an inline TypeScript snippet without a file and returns its stdout.

      **Command**
      ```
      deno eval "console.log(JSON.stringify({ hello: \"world\", time: new Date().toISOString() }))"
      ```

      Useful for quick one-liners or testing snippets that don't need a file on disk.

      **Example response**
      ```json
      {"hello":"world","time":"2024-01-15T10:30:00.000Z"}
      ```
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/test", async () =>
    {
      var output = await Deno.Test<string>(
        files: ["hello_test.ts"],
        baseOptions: baseOpts);
      return Results.Text(output);
    })
    .WithName("CommandTest")
    .WithSummary("deno test")
    .WithDescription("""
      Runs the Deno test suite for a specific file and returns the full test runner output.

      **Command**
      ```
      deno test hello_test.ts
      ```

      **Working directory:** `scripts/`

      The response contains the raw test runner output including pass/fail status,
      test names, durations, and any assertion errors.

      **Example response**
      ```
      running 1 test from ./hello_test.ts
      hello test ... ok (2ms)

      ok | 1 passed | 0 failed (10ms)
      ```
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/bench", async () =>
    {
      var output = await Deno.Bench<string>(
        files: ["hello_bench.ts"],
        baseOptions: baseOpts);
      return Results.Text(output);
    })
    .WithName("CommandBench")
    .WithSummary("deno bench")
    .WithDescription("""
      Runs Deno benchmarks and returns the results table.

      **Command**
      ```
      deno bench hello_bench.ts
      ```

      **Working directory:** `scripts/`

      Deno's benchmark runner executes each `Deno.bench()` block repeatedly and reports
      iterations/second, average time, min/max, and standard deviation.

      **Example response**
      ```
      cpu: 13th Gen Intel(R) Core(TM) i7-13700H
      runtime: deno 2.9.0 (x86_64-pc-windows-msvc)

      benchmark      time/iter (avg)  iter/s
      -------------- ---------------  ------
      hello bench           250 ns    4,000,000
      ```
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/fmt", async () =>
    {
      var output = await Deno.Fmt<string>(
        files: ["app.ts"],
        options: new FmtOptions { Check = true },
        baseOptions: baseOpts);
      return Results.Text(string.IsNullOrWhiteSpace(output) ? "Formatting OK" : output);
    })
    .WithName("CommandFmt")
    .WithSummary("deno fmt --check")
    .WithDescription("""
      Checks whether `app.ts` is correctly formatted — **read-only, no files are modified**.

      **Command**
      ```
      deno fmt --check app.ts
      ```

      **Working directory:** `scripts/`

      `--check` makes the formatter exit with an error if any file would be changed,
      without actually writing any changes. Returns `"Formatting OK"` when the file is clean,
      or the formatter diff output if not.
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/lint", async () =>
    {
      var output = await Deno.Lint<string>(
        files: ["app.ts"],
        baseOptions: baseOpts);
      return Results.Text(string.IsNullOrWhiteSpace(output) ? "No lint issues" : output);
    })
    .WithName("CommandLint")
    .WithSummary("deno lint")
    .WithDescription("""
      Runs Deno's built-in linter on `app.ts` and returns any findings.

      **Command**
      ```
      deno lint app.ts
      ```

      **Working directory:** `scripts/`

      Deno's linter enforces best practices (no `var`, no `debugger`, explicit return types, etc.)
      using a set of built-in rules. Returns `"No lint issues"` when clean,
      or the list of rule violations with file positions if not.
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/check", async () =>
    {
      var output = await Deno.Check<string>(
        files: ["app.ts"],
        baseOptions: baseOpts);
      return Results.Text(string.IsNullOrWhiteSpace(output) ? "Type check OK" : output);
    })
    .WithName("CommandCheck")
    .WithSummary("deno check")
    .WithDescription("""
      Type-checks `app.ts` using the TypeScript compiler embedded in Deno — **no code is executed**.

      **Command**
      ```
      deno check app.ts
      ```

      **Working directory:** `scripts/`

      Unlike `deno run`, this only performs static type analysis.
      Returns `"Type check OK"` on success, or TypeScript compiler diagnostics on failure.
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/compile", async () =>
    {
      var outFile = Path.Combine(Path.GetTempPath(),
        OperatingSystem.IsWindows() ? "deno-example.exe" : "deno-example");
      try
      {
        var output = await Deno.Compile<string>("app.ts",
          options: new CompileOptions { Output = outFile, NoCheck = "" },
          baseOptions: baseOpts);
        return Results.Text($"Compiled to {outFile}\n{output}".TrimEnd());
      }
      finally
      {
        if (File.Exists(outFile)) File.Delete(outFile);
      }
    })
    .WithName("CommandCompile")
    .WithSummary("deno compile")
    .WithDescription("""
      Compiles `app.ts` into a self-contained executable and immediately deletes it afterwards.

      **Command**
      ```
      deno compile --no-check --output <temp-path>/deno-example[.exe] app.ts
      ```

      **Working directory:** `scripts/`

      `--no-check` skips type-checking to speed up compilation.
      The output binary bundles the Deno runtime and your script into a single file
      that runs without Deno installed. The file is written to the system temp directory
      and deleted after the response is sent — this endpoint only demonstrates the compile step.
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/task", async () =>
    {
      var output = await Deno.Task<string>("hello", baseOptions: baseOpts);
      return Results.Text(output);
    })
    .WithName("CommandTask")
    .WithSummary("deno task hello")
    .WithDescription("""
      Runs the `hello` task defined in `scripts/deno.json` and returns its output.

      **Command**
      ```
      deno task hello
      ```

      **Working directory:** `scripts/`

      `deno task` is Deno's built-in task runner (similar to npm scripts).
      The task definition lives in `scripts/deno.json` under the `"tasks"` key.
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/serve", async () =>
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
      try
      {
        await Deno.Serve("serve_handler.ts",
          options: new ServeOptions { Port = 18080, AllowNet = [] },
          baseOptions: baseOpts,
          cancellationToken: cts.Token);
      }
      catch (Exception) when (cts.IsCancellationRequested) { }
      return Results.Text("deno serve started and was stopped after 2 s (long-running by design).");
    })
    .WithName("CommandServe")
    .WithSummary("deno serve (2 s demo)")
    .WithDescription("""
      Starts an HTTP server from `serve_handler.ts` via `deno serve`, then stops it after **2 seconds**.

      **Command**
      ```
      deno serve --port 18080 --allow-net serve_handler.ts
      ```

      **Working directory:** `scripts/`

      `deno serve` is designed for long-running HTTP servers that export a `default` object
      with a `fetch` handler. This endpoint starts the server on port `18080` and cancels it
      after 2 s to demonstrate the `CancellationToken` integration in `DenoProcess`.

      > **Note:** `deno serve` is inherently long-running. In production you would keep it alive
      > for the lifetime of your application, not cancel it after a fixed timeout.
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/cache", async () =>
    {
      var output = await Deno.Cache<string>(
        files: ["app.ts"],
        baseOptions: baseOpts);
      return Results.Text(string.IsNullOrWhiteSpace(output) ? "Cache OK" : output);
    })
    .WithName("CommandCache")
    .WithSummary("deno cache")
    .WithDescription("""
      Downloads and caches all remote dependencies of `app.ts` without executing it.

      **Command**
      ```
      deno cache app.ts
      ```

      **Working directory:** `scripts/`

      Resolves all `import` statements, downloads missing modules from JSR/npm/HTTPS,
      and stores them in Deno's local module cache. Subsequent runs are faster because
      no network access is needed. Returns `"Cache OK"` when all dependencies are resolved.
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/add", async () =>
    {
      var tempDir = Directory.CreateTempSubdirectory("deno-add-").FullName;
      try
      {
        await File.WriteAllTextAsync(Path.Combine(tempDir, "deno.json"), """{"imports":{}}""");
        var output = await Deno.Add<string>(
          packages: ["jsr:@std/assert@^1.0.0"],
          baseOptions: new DenoExecuteBaseOptions { WorkingDirectory = tempDir });
        return Results.Text(string.IsNullOrWhiteSpace(output) ? "Package added" : output);
      }
      finally
      {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
      }
    })
    .WithName("CommandAdd")
    .WithSummary("deno add")
    .WithDescription("""
      Adds a package to a `deno.json` import map — demonstrated in an isolated temp directory.

      **Command**
      ```
      deno add jsr:@std/assert@^1.0.0
      ```

      **Working directory:** temporary directory (deleted after the request)

      A fresh `deno.json` with an empty `imports` map is created in a temp directory so that
      `deno add` does not modify the project's own `deno.json`. After the request completes,
      the temp directory is deleted. This demonstrates how `Deno.Add` works — in a real application
      you would run this against your actual project directory.
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    app.MapGet("/commands/remove", async () =>
    {
      var tempDir = Directory.CreateTempSubdirectory("deno-remove-").FullName;
      try
      {
        await File.WriteAllTextAsync(Path.Combine(tempDir, "deno.json"),
          """{"imports":{"@std/assert":"jsr:@std/assert@^1.0.0"}}""");
        var output = await Deno.Remove<string>(
          packages: ["@std/assert"],
          baseOptions: new DenoExecuteBaseOptions { WorkingDirectory = tempDir });
        return Results.Text(string.IsNullOrWhiteSpace(output) ? "Package removed" : output);
      }
      finally
      {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
      }
    })
    .WithName("CommandRemove")
    .WithSummary("deno remove")
    .WithDescription("""
      Removes a package from a `deno.json` import map — demonstrated in an isolated temp directory.

      **Command**
      ```
      deno remove @std/assert
      ```

      **Working directory:** temporary directory (deleted after the request)

      A temp `deno.json` is pre-populated with `@std/assert` so `deno remove` has something to
      remove. The directory is deleted after the request. This is the counterpart to `/commands/add`
      and demonstrates `Deno.Remove` without touching the project's own import map.
      """)
    .Produces<string>(200, "text/plain")
    .ProducesProblem(500)
    .WithTags("Commands");

    return app;
  }
}
