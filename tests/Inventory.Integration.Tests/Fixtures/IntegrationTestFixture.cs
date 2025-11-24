using DotNet.Testcontainers.Builders;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Inventory.Integration.Tests.Fixtures;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private MsSqlContainer? _sqlContainer;
    private RabbitMqContainer? _rabbitMqContainer;

    public string SqlConnectionString { get; private set; } = string.Empty;
    public string RabbitMqConnectionString { get; private set; } = string.Empty;
    public int RabbitMqPort { get; private set; }

    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong@Passw0rd")
            .WithExposedPort(1433)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();

        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management")
            // create a non-guest user to allow connections from host-mapped ports
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "test")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", "testpwd")
            .WithExposedPort(5672)
            .WithExposedPort(15672)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
            .Build();

        await Task.WhenAll(_sqlContainer.StartAsync(), _rabbitMqContainer.StartAsync());

        SqlConnectionString = _sqlContainer.GetConnectionString();
        RabbitMqConnectionString = _rabbitMqContainer.GetConnectionString();
        RabbitMqPort = _rabbitMqContainer.GetMappedPublicPort(5672);

        await ApplyMigrationsAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseSqlServer(SqlConnectionString);

        await using var context = new InventoryDbContext(optionsBuilder.Options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sqlContainer != null)
            await _sqlContainer.DisposeAsync();
        
        if (_rabbitMqContainer != null)
            await _rabbitMqContainer.DisposeAsync();
    }
}
