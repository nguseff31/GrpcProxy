using GrpcProxy.Server.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace GrpcProxy.Server.Tcp
{
    class LeastConnectedBalancer : ITcpBalancer
    {
        private List<TcpEndpoint> Endpoints;
        private object _lock = new object();

        public LeastConnectedBalancer(IEnumerable<EndpointConfig> endpointConfigs)
        {
            Endpoints = endpointConfigs.Select(c => new TcpEndpoint(c)).ToList();
        }
        public TcpEndpoint GetEndpoint()
        {
            lock (_lock)
            {
                TcpEndpoint leastConnectionsEndpoint = Endpoints.FirstOrDefault(e => e.IsAlive);

                for (int i = 0; i < Endpoints.Count; i++)
                {
                    if (!Endpoints[i].IsAlive) continue;

                    if (leastConnectionsEndpoint.ActiveConnections > Endpoints[i].ActiveConnections)
                    {
                        leastConnectionsEndpoint = Endpoints[i];
                    }
                }

                if (leastConnectionsEndpoint != null && leastConnectionsEndpoint.IsAlive)
                {
                    leastConnectionsEndpoint.Enter();
                }
                return leastConnectionsEndpoint;
            }
        }
    }
}
