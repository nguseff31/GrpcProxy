using NLog;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GrpcProxy.Server.Tcp
{
    class TcpProxyServer : IDisposable
    {
        private TcpListener listener;

        private readonly TcpBalancer _balancer;
        private ILogger _logger = LogManager.GetCurrentClassLogger();

        public TcpProxyServer(TcpBalancer balancer)
        {
            _balancer = balancer;
        }

        public Task Start(int port, CancellationToken ct)
        {
            if (listener != null && listener.Server.Connected)
            {
                throw new ApplicationException("Tcp proxy is already started");
            }

            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            _logger.Info($"Proxy is listening on port: {port}");

            ct.Register(() => listener.Stop());
            return Task.Run(async () => {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        
                        client.NoDelay = true;
                        _logger.Debug($"{client.Client.RemoteEndPoint} client connected");

                        _ = Task.Run(async () => {
                            using (client) { await ReceiveAsync(client, ct); }
                        });
                    }
                    catch(ObjectDisposedException ex)
                    {
                        throw new OperationCanceledException(null, ex, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex);
                    }
                }
            });
        }


        private async Task ReceiveAsync(TcpClient client, CancellationToken ct)
        {

            using var endpointConnection = _balancer.GetEndpoint();
            if (!endpointConnection.HasValue)
                throw new Exception("No alive servers left");
            var endpoint = endpointConnection.Value.Endpoint;

            _logger.Debug($"{client.Client.RemoteEndPoint}/{endpoint} endpoint selected");

            using var server = new TcpClient();
            try
            {
                await server.ConnectAsync(endpoint.Config.Address, endpoint.Config.Port);
            } catch(SocketException)
            {
                endpoint.Error();
                throw;
            }
            
            _logger.Debug($"{endpoint} have {endpoint.ActiveConnections} active connections");

            try
            {
                using (var clientStream = client.GetStream())
                using (var serverStream = server.GetStream())
                {
                    await Task.WhenAny(
                        serverStream.CopyToAsync(clientStream, ct),
                        clientStream.CopyToAsync(serverStream, ct));
                }
                _logger.Debug($"{client.Client.RemoteEndPoint}/{endpoint} connection closed");
            } catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private async Task WriteToPipe(NetworkStream clientStream, PipeWriter writer, CancellationToken ct)
        {
            const int minimumBufferSize = 512;

            while (!ct.IsCancellationRequested)
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await clientStream.ReadAsync(memory, ct);
                    
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    break;
                }

                var result = await writer.FlushAsync(ct);

                if (result.IsCompleted)
                {
                    break;
                }
            }
            await writer.CompleteAsync();
        }

        private async Task WriteToStream(NetworkStream serverStream, PipeReader reader, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(ct);
                ReadOnlySequence<byte> buffer = result.Buffer;
                reader.AdvanceTo(buffer.Start, buffer.End);

                foreach (var segment in result.Buffer)
                {
                    await serverStream.WriteAsync(segment, ct);
                }

                if (result.IsCompleted)
                {
                    break;
                }
            }

            await reader.CompleteAsync();
        }

        #region IDisposable
        private bool _disposed = false;

        ~TcpProxyServer() => Dispose();

        public void Dispose()
        {
            DisposeInternal();
            GC.SuppressFinalize(this);
        }

        protected virtual void DisposeInternal()
        {
            if (_disposed)
            {
                return;
            }

            listener.Stop();
            listener = null;

            _disposed = true;
        }
        #endregion
    }
}
