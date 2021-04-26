using GrpcProxy.Server.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GrpcProxy.Server.Tcp.Balancers
{
    class RoundRobinBalancer : ITcpBalancer
    {
        private List<TcpEndpoint> Endpoints;
        private int RoundRobinIndex;

        public RoundRobinBalancer(IEnumerable<EndpointConfig> endpointConfigs)
        {
            Endpoints = endpointConfigs.Select(c => new TcpEndpoint(c)).ToList();
        }

        public TcpEndpoint GetEndpoint()
        {
            for (int i = 0; i < Endpoints.Count; i++)
            {
                Interlocked.Increment(ref RoundRobinIndex);
                var ep = Endpoints[RoundRobinIndex % Endpoints.Count];
                if (ep.IsAlive)
                {
                    ep.Enter();
                    return ep;
                }
            }
            return null;
        }
    }
}
