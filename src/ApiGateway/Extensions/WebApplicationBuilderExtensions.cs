using Microsoft.AspNetCore.Builder;
using Serilog;

namespace ApiGateway.Extensions;

internal static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder ConfigureGatewayLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
                .WriteTo.Console(formatter: new Serilog.Formatting.Json.JsonFormatter());
        });

        return builder;
    }
}
