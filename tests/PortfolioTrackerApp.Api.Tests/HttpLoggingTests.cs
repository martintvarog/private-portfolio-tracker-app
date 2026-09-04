using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PortfolioTrackerApp.Connectors.Contracts;

namespace PortfolioTrackerApp.Api.Tests;

/// <summary>
/// Hosts the real app in-process (real Program.cs, real appsettings.json) with the bank swapped for
/// a fake, and captures every log line. Guards the inbound request log: method/path/status/duration
/// are logged; the request body (credential) and response body (IBAN, balances) never are.
/// </summary>
public class HttpLoggingTests
{
    private const string Credential = "SECRET-TOKEN-abc123";
    private const string Iban = "CZ7920100000002400222222";

    private sealed class FakeConnector : IConnector
    {
        public string SourceId => "fio";
        public Task<ConnectorSyncResult> FetchHoldingsAsync(string credential, CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectorSyncResult
            {
                Source = "fio",
                Status = SyncStatus.Ok,
                AccountLabel = Iban,
                Holdings = [new SyncedHolding { Kind = "cash", Symbol = "CZK", Quantity = 195.01m, Currency = "CZK" }],
            });
    }

    /// <summary>A connector with a programming error. The message deliberately may or may not carry the credential.</summary>
    private sealed class ThrowingConnector(bool leakCredentialInMessage) : IConnector
    {
        public string SourceId => "fio";
        public Task<ConnectorSyncResult> FetchHoldingsAsync(string credential, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(leakCredentialInMessage ? $"boom for {credential}" : "boom");
    }

    // Production, like Azure: no developer exception page. (WebApplicationFactory defaults to Development.)
    private static (WebApplicationFactory<Program> App, CapturingLoggerProvider Logs) CreateApp(IConnector? connector = null, string environment = "Production")
    {
        var logs = new CapturingLoggerProvider();
        var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureLogging(logging => logging.AddProvider(logs)); // appsettings filters still apply
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IConnector>();
                services.AddSingleton(connector ?? new FakeConnector());
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
        Assert.Contains(Iban, body); // the client DOES get the data...

        Assert.NotEmpty(lines);
        foreach (var (category, _, message) in lines) // ...the logs never do
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

        // Client: a clean RFC 7807 500, no stack trace, no internals.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("boom", body);
        Assert.DoesNotContain("InvalidOperationException", body);

        // Logs: the request log records the REAL status, and the crash is visible exactly once.
        var request = Assert.Single(lines, l => l.Category.StartsWith("Microsoft.AspNetCore.HttpLogging"));
        Assert.Contains("500", request.Message);
        var error = Assert.Single(lines, l => l.Level == LogLevel.Error);
        Assert.Contains("InvalidOperationException", error.Message);
        Assert.Contains("boom", error.Message);

        // And the law still holds: the credential is nowhere.
        Assert.All(lines, l => Assert.DoesNotContain(Credential, l.Message));
    }

    [Fact]
    public async Task Control_a_credential_inside_an_exception_message_DOES_reach_the_logs()
    {
        // Proves the IConnector law "never put the credential in an exception message" is not theoretical:
        // the framework logs unhandled exceptions verbatim. Nothing in Program.cs can undo that.
        var (response, lines) = await PostSync(new ThrowingConnector(leakCredentialInMessage: true));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(Credential, await response.Content.ReadAsStringAsync()); // the client still gets nothing
        var error = Assert.Single(lines, l => l.Level == LogLevel.Error);
        Assert.Contains(Credential, error.Message);
    }
}
