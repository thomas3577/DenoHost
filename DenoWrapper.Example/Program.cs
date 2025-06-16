using DenoWrapper;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/build", static async (HttpContext context) =>
{
    string command = "deno run --allow-read --allow-write --allow-net --allow-env --allow-run ./build.ts";

    await Deno.Execute(command);
})
.WithName("Build");

app.Run();
