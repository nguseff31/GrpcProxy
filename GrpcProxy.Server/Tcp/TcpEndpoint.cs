using GrpcProxy.Server.Configuration;
using System;
using System.Threading;

namespace GrpcProxy.Server.Tcp
{
    class TcpEndpoint
    {
        public EndpointConfig Config { get; }

        public int ActiveConnections => _activeConnections;
        public bool IsAlive { get; private set; } = true;

        private int _activeConnections;

        private object _errorLock = new object();

        public TcpEndpoint(EndpointConfig config)
        {
            Config = config;
        }

        public Connection Connect()
        {
            return new Connection(this);
        }

        private void Release()
        {
            Interlocked.Decrement(ref _activeConnections);
        }

        private void Enter()
        {
            Interlocked.Increment(ref _activeConnections);
        }

        public void Error()
        {
            lock (_errorLock)
            {
                IsAlive = false;
            }
        }

        public override string ToString()
        {
            return $"{Config.Address}:{Config.Port}";
        }


        public struct Connection : IDisposable
        {
            public readonly TcpEndpoint Endpoint;

            public Connection(TcpEndpoint endpoint)
            {
                if (endpoint == null)
                    throw new ArgumentException();

                Endpoint = endpoint;
                Endpoint.Enter();
            }

            public void Dispose()
            {
                Endpoint.Release();
            }
        }
    }
}
