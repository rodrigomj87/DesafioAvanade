using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Sales.Infrastructure.Observability;

public sealed class SalesMetrics : IDisposable
{
    private readonly Counter<long> _ordersCreatedCounter;
    private readonly Counter<long> _ordersFailedCounter;
    private readonly Meter? _ownedMeter;

    public SalesMetrics(IMeterFactory? meterFactory = null)
    {
        var meter = meterFactory?.Create(SalesTelemetry.MeterName) ?? new Meter(SalesTelemetry.MeterName);
        if (meterFactory is null)
        {
            _ownedMeter = meter;
        }

        _ordersCreatedCounter = meter.CreateCounter<long>(
            name: "orders_created_total",
            description: "Total de pedidos criados com sucesso");

        _ordersFailedCounter = meter.CreateCounter<long>(
            name: "orders_failed_total",
            description: "Total de pedidos que falharam durante a criação");
    }

    public void TrackOrderCreated(Guid orderId, string customerId)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("order_id", orderId.ToString()),
            new("customer_id", customerId)
        };

        _ordersCreatedCounter.Add(1, tags);
    }

    public void TrackOrderFailed(string failureReason)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("reason", failureReason)
        };

        _ordersFailedCounter.Add(1, tags);
    }

    public void Dispose()
    {
        _ownedMeter?.Dispose();
    }
}
