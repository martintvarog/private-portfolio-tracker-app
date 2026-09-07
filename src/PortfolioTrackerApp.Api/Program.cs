using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpLogging;
using PortfolioTrackerApp.Api.Sync;
using PortfolioTrackerApp.Connectors;

var builder = WebApplication.CreateBuilder(args);

// Enums as strings on the wire ("Ok", "InvalidCredential") — self-describing
// for clients and log-safe; numeric values stay an implementation detail.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// Unhandled exceptions → RFC 7807 500 with no internals, logged once at Error by the framework.
builder.Services.AddProblemDetails();

// Inbound request log: method, path, status, duration — and NOTHING else. The sync
// request body carries the user's bank credential and the response body their IBAN
// and balances, so RequestBody/ResponseBody/headers must never be added here.
// (Outbound calls to banks are a separate pipeline; see AddConnectorsModule.)
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
                          | HttpLoggingFields.RequestPath
                          | HttpLoggingFields.ResponseStatusCode
                          | HttpLoggingFields.Duration;
    options.CombineLogs = true; // one line per request instead of request + response lines
});

builder.Services.AddConnectorsModule();
// builder.Services.AddMarketDataModule(builder.Configuration);

var app = builder.Build();

// Order matters: HttpLogging is OUTERMOST so it sees the final status. The exception handler
// inside it turns a crash into a 500 before the request log is written — otherwise the request
// log says 200 for a crash and, in Production, the crash leaves no log line at all.
app.UseHttpLogging();

// Correlation: ASP.NET's TraceIdentifier is already attached to every log line of a request as the
// "RequestId" scope (Console IncludeScopes=true, JSON formatter). Hand the same id to the client so a
// user can quote it — it identifies the request, not the user.
app.Use(async (context, next) =>
{
    // OnStarting (not a direct header write): the exception handler calls Response.Clear() on a
    // crash, which would drop a header set up front. This callback runs right before headers go out.
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
        return Task.CompletedTask;
    });
    await next();
});

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Cache policy for the SPA: index.html must be revalidated on every load so a deploy reaches users
// without a hard refresh; the hashed assets/* it references change name per build and can be cached
// for a year. Without this, browsers keep a stale index.html → stale bundle after every deploy.
// The SAME options go to the fallback below — MapFallbackToFile runs its own static-file handler.
var spaFiles = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = ctx.File.Name == "index.html"
            ? "no-cache"
            : "public, max-age=31536000, immutable",
};
app.UseDefaultFiles();
app.UseStaticFiles(spaFiles);
app.MapFallbackToFile("index.html", spaFiles);

app.MapGroup("/api").MapSyncEndpoints();


app.Run();

// Lets the API test project host the app in-process (WebApplicationFactory<Program>).
public partial class Program;
