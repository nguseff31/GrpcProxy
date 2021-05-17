using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace GrpcProxy.Server
{
    public static class ConfigurationHelpers
    {
        public static IConfiguration BuildConfiguration()
        {
            var cb = new ConfigurationBuilder();
            cb.AddJsonFile("appsettings.json");
            cb.AddJsonFile("appsettings.local.json", optional: true); 
            cb.AddEnvironmentVariables();
            return cb.Build();
        }
    }
}
