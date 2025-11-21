using Inventory.Domain.Enums;

namespace Inventory.Application.Contracts;

public sealed record ProductQueryParameters(
    int Page = 1,
    int PageSize = 20,
    string? Sku = null,
    ProductStatus? Status = null,
    string? Name = null)
{
    public int Page { get; init; } = Page < 1 ? 1 : Page;
    public int PageSize { get; init; } = PageSize < 1 ? 20 : (PageSize > 100 ? 100 : PageSize);
}
