namespace GrpcProxy.Server.Tcp
{
    abstract class TcpBalancer
    {
        public abstract TcpEndpoint.Connection? GetEndpoint();
    }
}
