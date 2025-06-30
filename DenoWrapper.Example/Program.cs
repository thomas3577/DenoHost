using DenoWrapper.Core;

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
  string[] args = ["--allow-read", "--allow-write", "--allow-net", "--allow-env", "--allow-run", "run.ts"];

  await Deno.Execute(cwd, command, args);
})
.WithName("Execute");

app.Run();


app.MapGet("/var/b", static async context =>
{
  string cwd = Path.Combine(Directory.GetCurrentDirectory(), SCRIPTS_PATH);

  Directory.SetCurrentDirectory(cwd);

  string command = "run";
  string[] args = ["--allow-read", "--allow-write", "--allow-net", "--allow-env", "--allow-run", "run.ts"];

  await Deno.Execute(command, args);
})
.WithName("Execute");
