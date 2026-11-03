using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using PortfolioTrackerApp.Api.Tests.LoggingTests;
using PortfolioTrackerApp.Connectors.Contracts;
using PortfolioTrackerApp.Connectors.Logging;

namespace PortfolioTrackerApp.Api.Tests;

public class HttpLoggingTests
{
    private const string Credential = "SECRET-TOKEN-abc123";
    private const string Iban = "CZ7920100000002400222222";

    private sealed class FakeConnector : IConnector
    {
        public string SourceId => "fio";
        public Task<ConnectorSyncResult> FetchPositionsAsync(string credential, CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectorSyncResult
            {
                Source = "fio",
                Status = SyncStatus.Ok,
                AccountLabel = Iban,
                Positions = [new SyncedPosition { AssetType = "cash", Symbol = "CZK", Quantity = 195.01m, Currency = "CZK" }],
            });
    }

    private sealed class ThrowingConnector(bool leakCredentialInMessage) : IConnector
    {
        public string SourceId => "fio";
        public Task<ConnectorSyncResult> FetchPositionsAsync(string credential, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(leakCredentialInMessage ? $"boom for {credential}" : "boom");
    }

    // wwwroot is gitignored build output, so CI has none.
    private static readonly string TestWebRoot = CreateTestWebRoot();

    private static string CreateTestWebRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "portfoliotracker-api-tests-wwwroot");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), "<!doctype html><title>test</title>");
        return dir;
    }

    private static (WebApplicationFactory<Program> App, CapturingLoggerProvider Logs) CreateApp(IConnector? connector = null, string environment = "Production")
    {
        var logs = new CapturingLoggerProvider();
        var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseWebRoot(TestWebRoot);
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IConnector>();
                services.AddTransient<IConnector>(sp => new LoggingConnector(
                    connector ?? new FakeConnector(),
                    sp.GetRequiredService<ILogger<LoggingConnector>>(),
                    sp.GetRequiredService<TimeProvider>()));
            });
        });
        return (app, logs);
    }

    private static async Task<(HttpResponseMessage Response, List<(string Category, LogLevel Level, string Message)> Lines)> PostSync(IConnector? connector = null)
    {
        var (app, logs) = CreateApp(connector);
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/sync", new { source = "fio", credential = Credential });

        await app.DisposeAsync();
        return (response, logs.Lines.ToList());
    }

    [Fact]
    public async Task Sync_request_is_logged_with_method_path_status_and_duration()
    {
        var (response, lines) = await PostSync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var line = Assert.Single(lines, l => l.Category.StartsWith("Microsoft.AspNetCore.HttpLogging"));
        Assert.Contains("POST", line.Message);
        Assert.Contains("/api/sync", line.Message);
        Assert.Contains("200", line.Message);
        Assert.Contains("Duration", line.Message);
    }

    [Fact]
    public async Task No_log_line_in_any_category_contains_the_credential_or_response_data()
    {
        var (response, lines) = await PostSync();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(Iban, body);

        Assert.NotEmpty(lines);
        foreach (var (category, _, message) in lines)
        {
            Assert.DoesNotContain(Credential, message);
            Assert.DoesNotContain(Iban, message);
            Assert.DoesNotContain("195.01", message);
            Assert.DoesNotContain("\"credential\"", message);
            Assert.DoesNotContain("closingBalance", message);
            Assert.False(message.Contains("RequestBody", StringComparison.Ordinal), $"Request body must not be logged: {category}: {message}");
            Assert.False(message.Contains("ResponseBody", StringComparison.Ordinal), $"Response body must not be logged: {category}: {message}");
        }
    }

    [Fact]
    public async Task Health_endpoint_is_logged_too_and_returns_ok()
    {
        var (app, logs) = CreateApp();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var line = Assert.Single(logs.Lines, l => l.Category.StartsWith("Microsoft.AspNetCore.HttpLogging"));
        Assert.Contains("/health", line.Message);
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Connector_exception_becomes_a_500_problem_details_and_is_logged_once_at_error()
    {
        var (response, lines) = await PostSync(new ThrowingConnector(leakCredentialInMessage: false));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("boom", body);
        Assert.DoesNotContain("InvalidOperationException", body);

        var request = Assert.Single(lines, l => l.Category.StartsWith("Microsoft.AspNetCore.HttpLogging"));
        Assert.Contains("500", request.Message);
        var error = Assert.Single(lines, l => l.Level == LogLevel.Error);
        Assert.Contains("InvalidOperationException", error.Message);
        Assert.Contains("boom", error.Message);

        Assert.All(lines, l => Assert.DoesNotContain(Credential, l.Message));
    }

    [Fact]
    public async Task Control_a_credential_inside_an_exception_message_DOES_reach_the_logs()
    {
        var (response, lines) = await PostSync(new ThrowingConnector(leakCredentialInMessage: true));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(Credential, await response.Content.ReadAsStringAsync());
        var error = Assert.Single(lines, l => l.Level == LogLevel.Error);
        Assert.Contains(Credential, error.Message);
    }

    [Fact]
    public async Task Response_carries_a_request_id_that_appears_in_both_the_request_and_outcome_log_lines()
    {
        var (response, lines) = await PostSync();

        var requestId = Assert.Single(response.Headers.GetValues("X-Request-Id"));
        Assert.False(string.IsNullOrWhiteSpace(requestId));

        var request = Assert.Single(lines, l => l.Category.StartsWith("Microsoft.AspNetCore.HttpLogging"));
        var outcome = Assert.Single(lines, l => l.Category.Contains("LoggingConnector"));
        Assert.Contains(requestId, request.Message);
        Assert.Contains(requestId, outcome.Message);
    }

    [Fact]
    public async Task Request_id_is_present_even_when_the_connector_crashes()
    {
        var (response, lines) = await PostSync(new ThrowingConnector(leakCredentialInMessage: false));

        var requestId = Assert.Single(response.Headers.GetValues("X-Request-Id"));
        var error = Assert.Single(lines, l => l.Level == LogLevel.Error);
        Assert.Contains(requestId, error.Message);
    }

    [Fact]
    public async Task Index_html_is_never_cached_but_hashed_assets_are_immutable()
    {
        var (app, _) = CreateApp();
        using var client = app.CreateClient();

        foreach (var path in new[] { "/", "/dashboard" })
        {
            var index = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, index.StatusCode);
            Assert.Equal("no-cache", index.Headers.CacheControl?.ToString());
        }
        await app.DisposeAsync();
    }
}
