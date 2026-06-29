using DenoHost.Core;
using DenoHost.Example;
using Scalar.AspNetCore;

const string SCRIPTS_PATH = "scripts";

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddOpenApi();

var app = builder.Build();

if (!app.Environment.IsProduction())
{
  app.UseHttpsRedirection();
}

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/", () => Results.Redirect("/scalar/v1"))
  .ExcludeFromDescription();

app.MapGet("/run-app", static async (HttpContext context) =>
{
  var command = "run";
  var workingDirectory = Path.Combine(Directory.GetCurrentDirectory(), SCRIPTS_PATH);
  var options = new DenoExecuteBaseOptions() { WorkingDirectory = workingDirectory };
  string[] args = ["app.ts"];
  var output = await Deno.Execute<string>(command, options, args);

  return Results.Text(output);
})
.WithName("RunAppTs")
.WithSummary("Run app.ts")
.WithDescription("""
  Executes `scripts/app.ts` via `Deno.Execute` and returns the captured stdout as plain text.

  **Command**
  ```
  deno run scripts/app.ts
  ```
  """)
.Produces<string>(200, "text/plain")
.ProducesProblem(500);

app.MapGet("/demo", static async (HttpContext context) =>
{
  var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

  try
  {
    logger.LogInformation("Starting DenoProcess demonstration...");
    await DenoProcessExample.BasicUsageExample(logger);
    return Results.Text("DenoProcess demonstration completed successfully. Check the logs for details.");
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Error during DenoProcess demonstration");
    return Results.Problem("Error occurred during demonstration. Check logs for details.");
  }
})
.WithName("Demo")
.WithSummary("DenoProcess basic usage demo")
.WithDescription("""
  Runs `DenoProcessExample.BasicUsageExample` which starts a Deno process via `DenoProcess`,
  reads its stdout line by line, and logs each line using the ASP.NET Core logger.

  Output is written to the **server console/log**, not to the HTTP response body.
  The response only confirms completion.
  """)
.Produces<string>(200, "text/plain")
.ProducesProblem(500);

app.MapGet("/demo/wait", static async (HttpContext context) =>
{
  var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

  try
  {
    await DenoProcessExample.WaitForExitExample(logger);
    return Results.Text("WaitForExit example completed.");
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Error during WaitForExit example");
    return Results.Problem("Error occurred during example.");
  }
})
.WithName("DemoWait")
.WithSummary("WaitForExit demo")
.WithDescription("""
  Demonstrates `DenoProcess.WaitForExitAsync()`.

  Starts a Deno process and awaits its natural completion rather than streaming output.
  Useful for short-lived scripts where you only care about the exit code.

  Output is written to the **server log**.
  """)
.Produces<string>(200, "text/plain")
.ProducesProblem(500);

app.MapGet("/demo/interactive", static async (HttpContext context) =>
{
  var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

  try
  {
    await DenoProcessExample.InteractiveExample(logger);
    return Results.Text("Interactive example completed.");
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Error during interactive example");
    return Results.Problem("Error occurred during example.");
  }
})
.WithName("DemoInteractive")
.WithSummary("Interactive process demo")
.WithDescription("""
  Demonstrates bidirectional stdin/stdout communication with a running Deno process via `DenoProcess`.

  The .NET host writes data to the process's stdin and reads responses from stdout —
  simulating an interactive script conversation without blocking the thread.

  Output is written to the **server log**.
  """)
.Produces<string>(200, "text/plain")
.ProducesProblem(500);

app.MapGet("/version", static async (HttpContext context) =>
{
  try
  {
    var command = "--version";
    var options = new DenoExecuteBaseOptions();
    var version = await Deno.Execute<string>(command, options);

    return Results.Text(version);
  }
  catch (Exception ex)
  {
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error retrieving Deno version");
    return Results.Problem("Error occurred while retrieving Deno version.");
  }
})
.WithName("GetVersion")
.WithSummary("Deno runtime version")
.WithDescription("""
  Returns the version string of the bundled Deno executable.

  **Command**
  ```
  deno --version
  ```

  **Example response**
  ```
  deno 2.9.0 (stable, release, x86_64-pc-windows-msvc)
  v8 13.7.152.6
  typescript 5.8.3
  ```
  """)
.Produces<string>(200, "text/plain")
.ProducesProblem(500);

app.MapCommandEndpoints();

app.Run();
