using GrpcProxy.Server.Configuration;
using System.Threading;

namespace GrpcProxy.Server.Tcp
{
    class TcpEndpoint
    {
        public EndpointConfig Config { get; }

        public int ActiveConnections;

        public bool IsAlive { get; private set; } = true;

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

        /// <summary>
        /// temporary removes
        /// </summary>
        public void Error()
        {
            lock (_errorLock)
            {
                IsAlive = false;
                Release();
            }
        }

        public override string ToString()
        {
            return $"{Config.Address}:{Config.Port}";
        }
    }
}
