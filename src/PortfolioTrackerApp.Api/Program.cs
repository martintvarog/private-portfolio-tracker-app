using System.Text.Json.Serialization;
using PortfolioTrackerApp.Api.Hosting;
using PortfolioTrackerApp.Api.Sync;
using PortfolioTrackerApp.Connectors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddLogSafeHttpLogging();
builder.Services.AddConnectorsModule();

var app = builder.Build();

app.UseHttpLogging();
app.UseRequestIdHeader();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapSpa();

app.MapGroup("/api").MapSyncEndpoints();

app.Run();

public partial class Program;
