using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sales.Application.Services;
using Sales.Application.Contracts;
using Sales.Application.Validators;
using Sales.Domain.Repositories;
using Sales.Infrastructure.Persistence;
using Sales.Infrastructure.Repositories;
using Sales.Infrastructure.Services;
using Sales.Infrastructure.Messaging;
using FluentValidation;

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

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderService, OrderService>();

        services.AddValidatorsFromAssemblyContaining<CreateOrderDtoValidator>();

        var inventoryServiceUrl = configuration["Services:Inventory"]
            ?? throw new InvalidOperationException("Configuration 'Services:Inventory' not found");

        services.AddTransient<Sales.Infrastructure.Http.AuthTokenDelegatingHandler>();

        services.AddHttpClient<IStockChecker, StockCheckerService>(client =>
        {
            client.BaseAddress = new Uri(inventoryServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(2);
        })
        .AddHttpMessageHandler<Sales.Infrastructure.Http.AuthTokenDelegatingHandler>();

        return services;
    }

    public static IServiceCollection AddRabbitMqPublisher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqSettings>(configuration.GetSection(RabbitMqSettings.SectionName));
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

        return services;
    }
}
