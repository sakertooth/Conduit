using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Conduit.Minecraft
{
    public class MinecraftResponse
    {
        public IPAddress Address { get; private set; }
        public int Port { get; private set; }
        public string Version { get; private set; }
        public int Online { get; private set; }
        public int Max { get; private set; }
        public string Description { get; private set; }

        public MinecraftResponse(IPAddress address, int port, string version, int online, int max, string description)
        {
            Address = address;
            Port = port;
            Version = version;
            Online = online;
            Max = max;
            Description = description;
        }

        public void Print()
        {
            Console.WriteLine($"{Address}:{Port} [{Version}] ({Online}/{Max}) {Description}");
        }
    }
}
