using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sales.Application.Services;
using Sales.Infrastructure.Persistence;
using Sales.Infrastructure.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class SalesInfrastructureExtensions
{
    public static IServiceCollection AddSalesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SalesDb")
            ?? throw new InvalidOperationException("Connection string 'SalesDb' not found.");

        services.AddDbContext<SalesDbContext>(options =>
            options.UseSqlServer(connectionString));

        var inventoryServiceUrl = configuration["Services:Inventory"]
            ?? throw new InvalidOperationException("Configuration 'Services:Inventory' not found");

        services.AddHttpClient<IStockChecker, StockCheckerService>(client =>
        {
            client.BaseAddress = new Uri(inventoryServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(2);
        });

        return services;
    }
}
