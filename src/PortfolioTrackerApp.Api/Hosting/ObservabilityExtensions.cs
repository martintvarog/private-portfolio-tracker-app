using Microsoft.AspNetCore.HttpLogging;

namespace PortfolioTrackerApp.Api.Hosting;

public static class ObservabilityExtensions
{
    public const string RequestIdHeader = "X-Request-Id";

    public static IServiceCollection AddLogSafeHttpLogging(this IServiceCollection services) =>
        services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestMethod
                                  | HttpLoggingFields.RequestPath
                                  | HttpLoggingFields.ResponseStatusCode
                                  | HttpLoggingFields.Duration;
            options.CombineLogs = true;
        });

    public static IApplicationBuilder UseRequestIdHeader(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[RequestIdHeader] = context.TraceIdentifier;
                return Task.CompletedTask;
            });
            await next();
        });
}
