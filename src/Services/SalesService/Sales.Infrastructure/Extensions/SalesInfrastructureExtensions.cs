using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sales.Infrastructure.Persistence;

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

        return services;
    }
}
