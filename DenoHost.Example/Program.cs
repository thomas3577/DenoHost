using DenoHost.Core;

const string SCRIPTS_PATH = "scripts";

var builder = WebApplication.CreateBuilder();
var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", static async (HttpContext context) =>
{
  var command = "run";
  var workingDirectory = Path.Combine(Directory.GetCurrentDirectory(), SCRIPTS_PATH);
  var options = new DenoExecuteBaseOptions() { WorkingDirectory = workingDirectory };
  string[] args = ["app.ts"];
  var output = await Deno.Execute<string>(command, options, args);

  return Results.Ok(output);
});

app.Run();
