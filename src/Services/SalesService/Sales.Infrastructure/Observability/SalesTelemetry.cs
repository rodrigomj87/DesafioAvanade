using System.Diagnostics;

namespace Sales.Infrastructure.Observability;

public static class SalesTelemetry
{
    public const string ActivitySourceName = "SalesService.Messaging";
    public const string MeterName = "SalesService.Metrics";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
