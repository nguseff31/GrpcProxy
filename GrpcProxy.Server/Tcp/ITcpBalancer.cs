using GrpcProxy.Server.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GrpcProxy.Server.Tcp
{
    interface ITcpBalancer
    {
        TcpEndpoint GetEndpoint();
    }

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
                if (ep.IsAlive()) return ep;
            }
            return null;
        }
    }

    class TcpEndpoint
    {
        public EndpointConfig Config { get; }

        public int ActiveConnections;

        private DateTime? LastError;
        private TimeSpan ErrorTimeout = TimeSpan.FromSeconds(30);

        private object _errorLock = new object();

        public TcpEndpoint(EndpointConfig config)
        {
            Config = config;
        }

        public void Release()
        {
            Interlocked.Decrement(ref ActiveConnections);
        }

        public void Enter()
        {
            Interlocked.Increment(ref ActiveConnections);
        }

        public void Error()
        {
            lock(_errorLock)
            {
                LastError = DateTime.UtcNow;
            }
        }

        public bool IsAlive()
        {
            if (!LastError.HasValue) return true;

            return DateTime.UtcNow > LastError + ErrorTimeout;
        }
    }
}
