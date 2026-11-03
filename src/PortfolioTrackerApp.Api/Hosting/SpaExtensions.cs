namespace PortfolioTrackerApp.Api.Hosting;

public static class SpaExtensions
{
    private const string IndexFile = "index.html";
    private const string RevalidateEveryLoad = "no-cache";
    private const string CacheForever = "public, max-age=31536000, immutable";

    public static WebApplication MapSpa(this WebApplication app)
    {
        var spaFiles = new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
                ctx.Context.Response.Headers.CacheControl =
                    ctx.File.Name == IndexFile ? RevalidateEveryLoad : CacheForever,
        };

        app.UseDefaultFiles();
        app.UseStaticFiles(spaFiles);
        app.MapFallbackToFile(IndexFile, spaFiles);
        return app;
    }
}
