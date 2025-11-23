using ApiGateway.Extensions;
using Microsoft.Extensions.DependencyInjection;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
var builder = WebApplication.CreateBuilder(args);

builder.ConfigureGatewayLogging();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services
    .AddGatewayObservability(builder.Configuration, builder.Environment.ApplicationName)
    .AddGatewayAuthentication(builder.Configuration)
    .AddGatewayRateLimiting(builder.Configuration)
    .AddGatewayReverseProxy();

var app = builder.Build();

app.UseGatewayDocumentation();
app.UseCorrelationId();
app.UseGatewayRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGatewayHealthEndpoints();
app.MapGatewayReverseProxy();

app.Run();

public partial class Program;
