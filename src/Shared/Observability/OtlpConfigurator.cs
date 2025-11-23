using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using System;

namespace Shared.Observability;

public static class OtlpConfigurator
{
    public static void ConfigureOtlpExporterForTraces(IConfiguration configuration, OtlpExporterOptions options)
    {
        ConfigureCommonOtlpOptions(configuration, options);

        options.Protocol = options.Protocol == default ? OtlpExportProtocol.HttpProtobuf : options.Protocol;

        if (options.Protocol == OtlpExportProtocol.HttpProtobuf)
        {
            var uri = options.Endpoint ?? new Uri("http://localhost:4318");
            var path = uri.AbsolutePath?.TrimEnd('/') ?? string.Empty;
            if (!path.EndsWith("/v1/traces", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new UriBuilder(uri) { Path = (path + "/v1/traces").TrimStart('/') };
                options.Endpoint = builder.Uri;
            }
        }
        else
        {
            options.Endpoint ??= new Uri("http://localhost:4317");
        }
    }

    public static void ConfigureOtlpExporterForMetrics(IConfiguration configuration, OtlpExporterOptions options)
    {
        ConfigureCommonOtlpOptions(configuration, options);

        options.Protocol = options.Protocol == default ? OtlpExportProtocol.HttpProtobuf : options.Protocol;

        if (options.Protocol == OtlpExportProtocol.HttpProtobuf)
        {
            var uri = options.Endpoint ?? new Uri("http://localhost:4318");
            var path = uri.AbsolutePath?.TrimEnd('/') ?? string.Empty;
            if (!path.EndsWith("/v1/metrics", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new UriBuilder(uri) { Path = (path + "/v1/metrics").TrimStart('/') };
                options.Endpoint = builder.Uri;
            }
        }
        else
        {
            options.Endpoint ??= new Uri("http://localhost:4317");
        }
    }

    public static void ConfigureCommonOtlpOptions(IConfiguration configuration, OtlpExporterOptions options)
    {
        var otlpSection = configuration.GetSection("OpenTelemetry:Otlp");

        if (otlpSection.Exists())
        {
            var endpoint = otlpSection.GetValue<string>("Endpoint");
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                options.Endpoint = new Uri(endpoint);
            }

            var headers = otlpSection.GetValue<string>("Headers");
            if (!string.IsNullOrWhiteSpace(headers))
            {
                options.Headers = headers;
            }

            var protocol = otlpSection.GetValue<string>("Protocol");
            if (!string.IsNullOrWhiteSpace(protocol) && Enum.TryParse<OtlpExportProtocol>(protocol, true, out var parsedProtocol))
            {
                options.Protocol = parsedProtocol;
            }
        }
    }
}
