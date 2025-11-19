using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace ApiGateway.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task InventoryRoute_ShouldReturn401_WhenTokenIsMissing()
    {
        await using var downstream = await DownstreamStub.StartAsync();
        await using var factory = new ApiGatewayFactory(downstream.BaseAddress, downstream.BaseAddress);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/inventory/api/v1/inventory/health");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InventoryRoute_ShouldForward_WhenTokenIsValid()
    {
        await using var downstream = await DownstreamStub.StartAsync();
        await using var factory = new ApiGatewayFactory(downstream.BaseAddress, downstream.BaseAddress);
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/inventory/api/v1/inventory/health");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtToken.CreateValidToken());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InventoryRoute_ShouldReturn401_WhenTokenExpired()
    {
        await using var downstream = await DownstreamStub.StartAsync();
        await using var factory = new ApiGatewayFactory(downstream.BaseAddress, downstream.BaseAddress);
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/inventory/api/v1/inventory/health");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtToken.CreateExpiredToken());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
