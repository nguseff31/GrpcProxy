using GrpcProxy.Server.Configuration;
using System.Collections.Generic;

namespace GrpcProxy.Server
{
    public class ProxyConfig
    {
        public int Port { get; set; }

        public List<EndpointConfig> Endpoints { get; set; } = new List<EndpointConfig>();
    }
}
