namespace Inventory.Domain.Exceptions;

public sealed class DuplicateSkuException : DomainException
{
    public string Sku { get; }

    public DuplicateSkuException(string sku)
        : base($"Produto com SKU '{sku}' já existe.")
    {
        Sku = sku;
    }
}
