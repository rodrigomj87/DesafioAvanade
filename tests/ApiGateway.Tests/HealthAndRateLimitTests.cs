using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace ApiGateway.Tests;

public sealed class HealthAndRateLimitTests
{
    [Fact]
    public async Task Healthz_ShouldReturn200_WithoutAuthorization()
    {
        await using var downstream = await DownstreamStub.StartAsync();
        await using var factory = new ApiGatewayFactory(downstream.BaseAddress, downstream.BaseAddress);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InventoryRoute_ShouldReturn429_WhenRateLimitExceeded()
    {
        await using var downstream = await DownstreamStub.StartAsync();
        await using var factory = new ApiGatewayFactory(downstream.BaseAddress, downstream.BaseAddress);
        using var client = factory.CreateClient();

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/inventory/api/v1/inventory/health");
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtToken.CreateValidToken());

        var firstResponse = await client.SendAsync(tokenRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/inventory/api/v1/inventory/health");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtToken.CreateValidToken());
        var secondResponse = await client.SendAsync(secondRequest);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var thirdRequest = new HttpRequestMessage(HttpMethod.Get, "/inventory/api/v1/inventory/health");
        thirdRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtToken.CreateValidToken());
        var thirdResponse = await client.SendAsync(thirdRequest);

        thirdResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
