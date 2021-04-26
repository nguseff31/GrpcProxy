using GrpcProxy.Server.Tcp;
using Microsoft.Extensions.Configuration;
using NLog.Config;
using NLog.Targets;
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
        static void Main(string[] args)
        {
            var configuration = ConfigurationHelpers.BuildConfiguration();
            var appConfig = new ProxyConfig();
            configuration.GetSection("Proxy").Bind(appConfig);

            var tcs = new CancellationTokenSource();
            Console.CancelKeyPress += (a, b) => tcs.Cancel();
            AppDomain.CurrentDomain.ProcessExit += (s, a) => tcs.Cancel();

            using var tcpServer = new TcpProxyServer(new LeastConnectedBalancer(appConfig.Endpoints));
            tcpServer.Start(appConfig.Port, tcs.Token);

            Console.WriteLine("Proxy is listening on port: " + appConfig.Port);
            Console.WriteLine("Servers:");
            foreach (var ep in appConfig.Endpoints)
            {
                Console.WriteLine(ep.Address + ":" + ep.Port);
            }

            Console.WriteLine("Press enter to stop proxy server");
            Console.ReadLine();
            
            tcs.Cancel();
        }
    }
}
