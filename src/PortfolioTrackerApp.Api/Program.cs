var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Module registration goes here as modules gain services:
// builder.Services.AddConnectorsModule(builder.Configuration);
// builder.Services.AddMarketDataModule(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
