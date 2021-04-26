using GrpcProxy.Server.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GrpcProxy.Server.Tcp
{
    interface ITcpBalancer
    {
        TcpEndpoint GetEndpoint();
    }

    

    
}
