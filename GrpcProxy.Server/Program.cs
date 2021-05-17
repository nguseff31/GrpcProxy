using GrpcProxy.Server.Tcp;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrpcProxy.Server
{
    class Program
    {
        const int CancellationTimeoutMs = 1000;

        static async Task Main()
        {
            var configuration = ConfigurationHelpers.BuildConfiguration();
            var appConfig = new ProxyConfig();
            configuration.GetSection("Proxy").Bind(appConfig);

            var tcs = new CancellationTokenSource();
            Console.CancelKeyPress += (a, b) => tcs.Cancel();
            AppDomain.CurrentDomain.ProcessExit += (s, a) => tcs.Cancel();

            var balancer = new LeastConnectedBalancer(appConfig.Endpoints);
            using var tcpServer = new TcpProxyServer(balancer);
            var listeningTask = tcpServer.Start(appConfig.Port, tcs.Token);

            Console.WriteLine("Proxy is listening on port: " + appConfig.Port);
            Console.WriteLine("Downstream servers:");
            foreach (var ep in appConfig.Endpoints)
            {
                Console.WriteLine(ep.Address + ":" + ep.Port);
            }

            Console.WriteLine("Press enter to stop proxy server");
            Console.ReadLine();
             
            tcs.Cancel();
            try
            {
                if (listeningTask != await Task.WhenAny(listeningTask, Task.Delay(CancellationTimeoutMs))) {
                    throw new TimeoutException();
                }
            } catch (OperationCanceledException) {}
        }
    }
}
