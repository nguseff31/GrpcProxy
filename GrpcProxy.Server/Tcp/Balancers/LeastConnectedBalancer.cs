using GrpcProxy.Server.Configuration;
using static MoreLinq.Extensions.MinByExtension;
using System.Collections.Generic;
using System.Linq;

namespace GrpcProxy.Server.Tcp
{
    class LeastConnectedBalancer : TcpBalancer
    {
        private List<TcpEndpoint> Endpoints;
        private object _lock = new object();

        public LeastConnectedBalancer(IEnumerable<EndpointConfig> endpointConfigs)
        {
            Endpoints = endpointConfigs.Select(c => new TcpEndpoint(c)).ToList();
        }

        public override TcpEndpoint.Connection? GetEndpoint()
        {
            lock (_lock)
            {
                var endpoint = Endpoints
                    .Where(e => e.IsAlive)
                    .MinBy(e => e.ActiveConnections)
                    .FirstOrDefault();
                
                if (endpoint != null)
                    return new TcpEndpoint.Connection(endpoint);
                return null;
            }
        }
    }
}
