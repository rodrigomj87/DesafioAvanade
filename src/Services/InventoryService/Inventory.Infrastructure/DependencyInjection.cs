using Inventory.Domain.Repositories;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class InventoryInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InventoryDatabase")
            ?? throw new InvalidOperationException("Connection string 'InventoryDatabase' not found.");

        services.AddDbContext<InventoryDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IStockMovementRepository, EfStockMovementRepository>();

        return services;
    }
}
