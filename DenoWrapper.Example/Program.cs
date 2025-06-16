var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.MapGet("/build", () =>
{
    // TODO(thu): Build the Deno project
    Deno.Execute();
    return;
})
.WithName("Build");

app.Run();
