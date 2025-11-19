using Inventory.Application.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class InventoryApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        services.AddScoped<ProductService>();
        return services;
    }
}
