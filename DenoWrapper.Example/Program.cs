using DenoWrapper;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", static async (HttpContext context) =>
{
  string command = "run";
  string[] args = { "--allow-read", "--allow-write", "--allow-net", "--allow-env", "--allow-run", "--config=./build/deno.json", "./build/run.ts" };

  await Deno.Execute(command, "A", args);
})
.WithName("Build");

app.Run();
