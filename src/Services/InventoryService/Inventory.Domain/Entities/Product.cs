using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

public sealed class Product
{
    private Product()
    {
        Sku = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
    }

    public Product(Guid id, string sku, string name, string description, decimal price, int quantityAvailable, int quantityReserved = 0, ProductStatus status = ProductStatus.Active)
    {
        Id = id;
        Sku = sku;
        Name = name;
        Description = description;
        Price = price;
        QuantityAvailable = quantityAvailable;
        QuantityReserved = quantityReserved;
        Status = status;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public string Sku { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }
    public int QuantityAvailable { get; private set; }
    public int QuantityReserved { get; private set; }
    public ProductStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void AdjustQuantity(int delta)
    {
        QuantityAvailable += delta;
        UpdatedAt = DateTime.UtcNow;
    }
}
