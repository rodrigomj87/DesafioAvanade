using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Inventory.Infrastructure.Observability;

public sealed class InventoryMetrics : IDisposable
{
    private readonly Counter<long> _stockUpdatesCounter;
    private readonly Histogram<double> _eventLatencyHistogram;
    private readonly Meter? _ownedMeter;

    public InventoryMetrics(IMeterFactory? meterFactory = null)
    {
        var meter = meterFactory?.Create(InventoryTelemetry.MeterName) ?? new Meter(InventoryTelemetry.MeterName);
        if (meterFactory is null)
        {
            _ownedMeter = meter;
        }
        _stockUpdatesCounter = meter.CreateCounter<long>(
            name: "stock_updates_total",
            description: "Total de movimentos de estoque aplicados a partir de eventos OrderConfirmed");

        _eventLatencyHistogram = meter.CreateHistogram<double>(
            name: "event_processing_duration_ms",
            unit: "ms",
            description: "Latência entre a criação do pedido e a atualização do estoque");
    }

    public void TrackStockUpdate(Guid productId, Guid orderId, int quantity)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("product_id", productId.ToString()),
            new("order_id", orderId.ToString()),
            new("quantity", quantity)
        };

        _stockUpdatesCounter.Add(1, tags);
    }

    public void TrackEventLatency(double elapsedMilliseconds, Guid orderId)
    {
        if (elapsedMilliseconds <= 0)
        {
            return;
        }

        var tags = new KeyValuePair<string, object?>[]
        {
            new("order_id", orderId.ToString())
        };

        _eventLatencyHistogram.Record(elapsedMilliseconds, tags);
    }

    public void Dispose()
    {
        _ownedMeter?.Dispose();
    }
}
