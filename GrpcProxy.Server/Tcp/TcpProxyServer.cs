using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GrpcProxy.Server.Tcp
{
    class TcpProxyServer : IDisposable
    {
        private TcpListener listener;

        private readonly ITcpBalancer _balancer;

        public TcpProxyServer(ITcpBalancer balancer)
        {
            _balancer = balancer;
        }

        public async Task Start(int port, CancellationToken ct)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            _ = Task.Run(() => {
                while (!ct.IsCancellationRequested)
                {
                    var client = listener.AcceptTcpClient();
                    client.NoDelay = true;

                    Trace.WriteLine("Connection accepted: " + client.Client.RemoteEndPoint.ToString());
                    _ = Task.Run(() => ReceiveAsync(client, ct));
                }
            });
        }


        private async Task ReceiveAsync(TcpClient client, CancellationToken ct)
        {
            var endpoint = _balancer.GetEndpoint();
            if (endpoint == null)
                throw new Exception("No alive servers left");

            Trace.WriteLine("Selected endpoint: " + endpoint.Config.Address + ":" + endpoint.Config.Port);
            TcpClient server = new TcpClient();
            server.NoDelay = true;

            try
            {
                ct.ThrowIfCancellationRequested();

                endpoint.Enter();
                await server.ConnectAsync(endpoint.Config.Address, endpoint.Config.Port);
                Trace.WriteLine($"Active connections on {endpoint.Config.Address}:{endpoint.Config.Port} - {endpoint.ActiveConnections}");

                using var clientStream = client.GetStream();
                using var serverStream = server.GetStream();

                await Task.WhenAny(
                    clientStream.CopyToAsync(serverStream, ct),
                    serverStream.CopyToAsync(clientStream, ct));
            }
            catch (Exception ex)
            {
                if (!server.Connected && client.Connected) // if server was disconnected
                {
                    Trace.WriteLine($"{endpoint.Config.Address}:{endpoint.Config.Port} server was disconnected");
                    endpoint.Error();
                }
                Trace.WriteLine(ex.Message);
            }
            finally
            {
                endpoint.Release();
                client.Close();
                server.Close();
            }
        }

        #region Disposable
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                listener?.Stop();
                listener = null;
            }
        }

        ~TcpProxyServer()
        {
            Dispose();
        }
        #endregion
    }
}
