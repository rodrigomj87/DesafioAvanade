using Inventory.Domain.Repositories;
using Inventory.Infrastructure.Repositories;

namespace Microsoft.Extensions.DependencyInjection;

public static class InventoryInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IProductRepository, InMemoryProductRepository>();
        return services;
    }
}
