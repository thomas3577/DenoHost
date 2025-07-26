using DenoHost.Core;
using DenoHost.Example;

const string SCRIPTS_PATH = "scripts";

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

app.UseHttpsRedirection();

// Original endpoint
app.MapGet("/", static async (HttpContext context) =>
{
  var command = "run";
  var workingDirectory = Path.Combine(Directory.GetCurrentDirectory(), SCRIPTS_PATH);
  var options = new DenoExecuteBaseOptions() { WorkingDirectory = workingDirectory };
  string[] args = ["app.ts"];
  var output = await Deno.Execute<string>(command, options, args);

  return Results.Text(output);
});

// New endpoint to demonstrate DenoProcess examples
app.MapGet("/demo", static async (HttpContext context) =>
{
  var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

  try
  {
    logger.LogInformation("Starting DenoProcess demonstration...");

    // Run basic functionality example
    await DenoProcessExample.BasicUsageExample(logger);

    return Results.Text("DenoProcess demonstration completed successfully. Check the logs for details.");
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Error during DenoProcess demonstration");
    return Results.Problem("Error occurred during demonstration. Check logs for details.");
  }
});

// Endpoint to run wait for exit example
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
});

// Endpoint to run interactive example
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
});

app.Run();
