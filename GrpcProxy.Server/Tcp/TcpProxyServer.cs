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

        const int BUFFER_SIZE = 4096;

        public TcpProxyServer(ITcpBalancer balancer)
        {
            _balancer = balancer;
        }

        public void Start(int port, CancellationToken ct)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            _ = Task.Run(() => {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var client = listener.AcceptTcpClient();
                        client.NoDelay = true;

                        Trace.WriteLine("Connection accepted: " + client.Client.RemoteEndPoint.ToString());
                        _ = Task.Run(() => ReceiveAsync(client, ct));
                    } catch (Exception ex)
                    {
                        Trace.WriteLine(ex.Message);
                    }
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

            try
            {
                await server.ConnectAsync(endpoint.Config.Address, endpoint.Config.Port);
            } catch (Exception ex)
            {
                Trace.WriteLine("Connection error:" + ex.Message);
                endpoint.Error();
                _ = Task.Run(() => ReceiveAsync(client, ct));
                return;
            }

            Trace.WriteLine($"Active connections on {endpoint.Config.Address}:{endpoint.Config.Port} - {endpoint.ActiveConnections}");

            var clientStream = client.GetStream();
            var serverStream = server.GetStream();
            try 
            {
                await Task.WhenAny(
                    clientStream.CopyToAsync(serverStream, ct),
                    serverStream.CopyToAsync(clientStream, ct));
            }
            catch (Exception ex)
            {
                endpoint.Error();
                Trace.WriteLine("Receive error:" + ex.Message);
            }
            finally
            {
                Trace.WriteLine($"Closing connection to: {endpoint.Config.Address}:{endpoint.Config.Port}");
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
                _disposed = true;
            }
        }

        ~TcpProxyServer()
        {
            Dispose();
        }
        #endregion
    }
}
