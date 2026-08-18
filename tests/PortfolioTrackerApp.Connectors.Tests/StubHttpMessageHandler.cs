using System.Net;
using System.Text;

namespace PortfolioTrackerApp.Connectors.Tests;

/// <summary>Returns a fixed response for every request — for testing any connector's HttpClient.</summary>
public sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
}
