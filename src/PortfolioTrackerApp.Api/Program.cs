using System.Text.Json.Serialization;
using PortfolioTrackerApp.Api.Sync;
using PortfolioTrackerApp.Connectors;

var builder = WebApplication.CreateBuilder(args);

// Enums as strings on the wire ("Ok", "InvalidCredential") — self-describing
// for clients and log-safe; numeric values stay an implementation detail.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

builder.Services.AddConnectorsModule();
// builder.Services.AddMarketDataModule(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapSyncEndpoints();

app.Run();
