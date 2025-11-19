using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.Tests;

internal sealed class DownstreamStub : IAsyncDisposable
{
    private readonly WebApplication _app;

    private DownstreamStub(WebApplication app)
    {
        _app = app;
    }

    public string BaseAddress => _app.Urls.Single();

    public static async Task<DownstreamStub> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        app.Map("/{**catch-all}", () => Results.Ok(new { status = "UP" }));

        await app.StartAsync();
        return new DownstreamStub(app);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
