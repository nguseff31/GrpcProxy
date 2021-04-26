using GrpcProxy.Server.Tcp;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GrpcProxy.Server
{
    class Program
    {
        static TcpListener listener;
        static ITcpBalancer _balancer;

        static void Main(string[] args)
        {
            var configuration = ConfigurationHelpers.BuildConfiguration();
            var appConfig = new ProxyConfig();
            configuration.GetSection("Proxy").Bind(appConfig);
            Trace.Listeners.Add(new ConsoleTraceListener());

            var tcs = new CancellationTokenSource();
            Console.CancelKeyPress += (a, b) => tcs.Cancel();

            var tcpServer = new TcpProxyServer(new LeastConnectedBalancer(appConfig.Endpoints));
            _ = tcpServer.Start(appConfig.Port, tcs.Token);

            Console.WriteLine("Proxy is listening on port: " + appConfig.Port);
            Console.WriteLine("Servers:");
            foreach (var ep in appConfig.Endpoints)
            {
                Console.WriteLine(ep.Address + ":" + ep.Port);
            }

            Console.ReadLine();
            tcs.Cancel();
            listener.Stop();
        }

        static async Task ReceiveAsync(TcpClient client, CancellationToken ct)
        {
            var endpoint = _balancer.GetEndpoint();
            if (endpoint == null)
                throw new Exception("No alive servers left");

            Trace.WriteLine("Selected endpoint: " + endpoint.Config.Address + ":" + endpoint.Config.Port);
            TcpClient server = new TcpClient();
            server.NoDelay = true;
            bool closeClient = true;

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
                if (!server.Connected)
                {
                    Trace.WriteLine($"{endpoint.Config.Address}:{endpoint.Config.Port} server was disconnected");
                    endpoint.Error();
                    closeClient = false;
                    _ = Task.Run(async () => await ReceiveAsync(client, ct)); // Select another server on error
                }
                Trace.WriteLine(ex.Message);
            }
            finally
            {
                endpoint.Release();
                Trace.WriteLine("Closing stream");
                if (closeClient) 
                    client.Close();
                server.Close();
            }
        }
    }
}
