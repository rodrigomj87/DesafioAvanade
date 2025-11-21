using Sales.Domain.Enums;

using Sales.Domain.ValueObjects;

namespace Sales.Domain.Entities;

public class Order
{
    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public Enums.OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<OrderItem> Items { get; private set; } = [];

    private Order() { }

    public static Order Create(CustomerId customerId, IEnumerable<OrderItem> items)
    {
        if (customerId is null)
            throw new ArgumentNullException(nameof(customerId));

        var itemsList = items?.ToList() ?? throw new ArgumentNullException(nameof(items));

        if (itemsList.Count == 0)
            throw new ArgumentException("Order must have at least one item", nameof(items));

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = Enums.OrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            Items = itemsList
        };

        order.CalculateTotalAmount();

        return order;
    }

    public void ConfirmOrder()
    {
        if (Status != Enums.OrderStatus.Created)
            throw new InvalidOperationException($"Cannot confirm order with status {Status}");

        Status = Enums.OrderStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CancelOrder()
    {
        if (Status == Enums.OrderStatus.Confirmed)
            throw new InvalidOperationException("Cannot cancel confirmed order");

        Status = Enums.OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        Status = Enums.OrderStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }

    private void CalculateTotalAmount()
    {
        TotalAmount = Items.Sum(item => item.TotalPrice);
    }
}
