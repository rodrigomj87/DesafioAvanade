using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace Sales.Infrastructure.Http;

public sealed class AuthTokenDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthTokenDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var token = httpContext?.Request.Headers["Authorization"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
