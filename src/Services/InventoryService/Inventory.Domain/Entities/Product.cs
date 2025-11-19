namespace Inventory.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; }
    public string Sku { get; }
    public string Name { get; }
    public string Description { get; }
    public decimal Price { get; }
    public int QuantityAvailable { get; private set; }

    public Product(Guid id, string sku, string name, string description, decimal price, int quantityAvailable)
    {
        Id = id;
        Sku = sku;
        Name = name;
        Description = description;
        Price = price;
        QuantityAvailable = quantityAvailable;
    }

    public void AdjustQuantity(int delta) => QuantityAvailable += delta;
}
