using GrpcProxy.Server.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GrpcProxy.Server.Tcp.Balancers
{
    class RoundRobinBalancer : TcpBalancer
    {
        private List<TcpEndpoint> Endpoints;
        private int RoundRobinIndex;

        public RoundRobinBalancer(IEnumerable<EndpointConfig> endpointConfigs)
        {
            Endpoints = endpointConfigs.Select(c => new TcpEndpoint(c)).ToList();
        }

        public override TcpEndpoint.Connection? GetEndpoint()
        {
            Interlocked.Increment(ref RoundRobinIndex);
            var ep = Endpoints[RoundRobinIndex % Endpoints.Count];
            if (ep.IsAlive)
            {
                return new TcpEndpoint.Connection(ep);
            }
            return null;
        }
    }
}
