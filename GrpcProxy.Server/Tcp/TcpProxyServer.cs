using NLog;
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
        private ILogger _logger = LogManager.GetCurrentClassLogger();

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

                        _logger.Debug($"Connection accepted: {client.Client.RemoteEndPoint}");
                        _ = Task.Run(() => ReceiveAsync(client, ct));
                    } catch (Exception ex)
                    {
                        _logger.Error(ex);
                    }
                }
            });
        }


        private async Task ReceiveAsync(TcpClient client, CancellationToken ct)
        {
            var endpoint = _balancer.GetEndpoint();
            if (endpoint == null)
                throw new Exception("No alive servers left");

            var logPrefix = $"{client.Client.RemoteEndPoint}/{endpoint}";

            _logger.Debug($"{logPrefix} endpoint selected");
            using var server = new TcpClient();

            try
            {
                await server.ConnectAsync(endpoint.Config.Address, endpoint.Config.Port);
            } catch (Exception ex)
            {
                _logger.Error(ex, $"{logPrefix} connect error");
                endpoint.Error();
                _ = Task.Run(() => ReceiveAsync(client, ct));
                return;
            }

            _logger.Debug($"{endpoint} - {endpoint.ActiveConnections} active connections");

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
                _logger.Error(ex, $"{logPrefix} stream copy error");
            }
            finally
            {
                _logger.Debug($"{logPrefix} - closing connection");
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
