using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.Tests;

internal sealed class ApiGatewayFactory(string inventoryAddress, string salesAddress) : WebApplicationFactory<Program>
{
    private readonly string _inventoryAddress = inventoryAddress;
    private readonly string _salesAddress = salesAddress;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var overrides = TestJwtToken.BuildConfiguration(_inventoryAddress, _salesAddress);
            configurationBuilder.AddInMemoryCollection(overrides);
        });
    }
}
