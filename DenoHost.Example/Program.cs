using DenoHost.Core;

const string SCRIPTS_PATH = "scripts";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.UseHttpsRedirection();

app.MapGet("/var/a", static async context =>
{
  string cwd = Path.Combine(Directory.GetCurrentDirectory(), SCRIPTS_PATH);
  string command = "run";
  string[] args = ["app.ts"];

  await Deno.Execute(cwd, command, args);
})
.WithName("Execute");

app.Run();
