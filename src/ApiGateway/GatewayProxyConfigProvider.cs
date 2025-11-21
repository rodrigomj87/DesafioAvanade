using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway;

internal sealed class GatewayProxyConfigProvider(IConfiguration configuration) : IProxyConfigProvider
{
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public IProxyConfig GetConfig()
    {
        var inventoryAddress = _configuration["Services:Inventory"] ?? "http://localhost:5101";
        var salesAddress = _configuration["Services:Sales"] ?? "http://localhost:5201";

        var routes = new[]
        {
            new RouteConfig
            {
                RouteId = "inventory-route",
                ClusterId = "inventory-cluster",
                Match = new RouteMatch { Path = "/inventory/{**catch-all}" },
                Transforms = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["PathPattern"] = "/api/v1/inventory/{**catch-all}"
                    }
                }
            },
            new RouteConfig
            {
                RouteId = "sales-route",
                ClusterId = "sales-cluster",
                Match = new RouteMatch { Path = "/sales/{**catch-all}" },
                Transforms = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["PathRemovePrefix"] = "/sales"
                    }
                }
            }
        };

        var clusters = new[]
        {
            new ClusterConfig
            {
                ClusterId = "inventory-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["inventory"] = new DestinationConfig { Address = inventoryAddress }
                }
            },
            new ClusterConfig
            {
                ClusterId = "sales-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["sales"] = new DestinationConfig { Address = salesAddress }
                }
            }
        };

        return new InMemoryConfig(routes, clusters);
    }

    private sealed class InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) : IProxyConfig
    {
        public IReadOnlyList<RouteConfig> Routes { get; } = routes;
        public IReadOnlyList<ClusterConfig> Clusters { get; } = clusters;
        public IChangeToken ChangeToken { get; } = new StaticChangeToken();

        private sealed class StaticChangeToken : IChangeToken
        {
            public bool HasChanged => false;
            public bool ActiveChangeCallbacks => false;

            public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => EmptyDisposable.Instance;

            private sealed class EmptyDisposable : IDisposable
            {
                public static readonly EmptyDisposable Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }
}
