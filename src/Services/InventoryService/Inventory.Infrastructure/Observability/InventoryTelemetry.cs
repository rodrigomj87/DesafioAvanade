using System.Diagnostics;

namespace Inventory.Infrastructure.Observability;

public static class InventoryTelemetry
{
    public const string ActivitySourceName = "InventoryService.Messaging";
    public const string MeterName = "InventoryService.Metrics";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
