using System.Net;

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
    }
}
