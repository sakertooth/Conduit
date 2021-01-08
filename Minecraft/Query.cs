using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Minecraft
{
    static class Query
    {
        public static async ValueTask<MinecraftResponse> SendQuery(IPAddress address, int port, int timeout)
        {
            using var client = new UdpClient();
            client.Connect(address, port);

            var sessionID = BitConverter.GetBytes((int)Stopwatch.GetTimestamp() & 0x0F0F0F0F);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(sessionID);
            }

            var challengeToken = await GetChallengeTokenAsync(client, sessionID, timeout);
            if (challengeToken == null)
            {
                return null;
            }

            var statRequestDatagram = new byte[11]
            {
                    0xFE, 0xFD,
                    0x00,
                    sessionID[0], sessionID[1], sessionID[2], sessionID[3],
                    challengeToken[0], challengeToken[1], challengeToken[2], challengeToken[3]
            };
            await client.SendAsync(statRequestDatagram, statRequestDatagram.Length);

            var statResponseDatagramTask = client.ReceiveAsync();
            using var statResponseDatagramCt = new CancellationTokenSource(timeout);

            if (await Task.WhenAny(statResponseDatagramTask, Task.Delay(timeout, statResponseDatagramCt.Token)) == statResponseDatagramTask)
            {
                var statResponseDatagram = await statResponseDatagramTask;
                using var statResponseStream = new MemoryStream(statResponseDatagram.Buffer);
                using var reader = new BinaryReader(statResponseStream, Encoding.ASCII);

                reader.ReadByte();
                reader.ReadInt32();
                var responseInfo = reader.ReadChars(statResponseDatagram.Buffer.Length - 5).ToString().Split('\0');

                if (responseInfo.Length < 5)
                {
                    return null;
                }

                return new MinecraftResponse(address, port, responseInfo[1], int.Parse(responseInfo[3]), int.Parse(responseInfo[4]));
            }
            else
            {
                return null;
            }

        }

        private static async Task<byte[]> GetChallengeTokenAsync(this UdpClient client, byte[] sessionID, int timeout)
        {
            var handshakeDatagram = new byte[7]
            {
                0xFE, 0xFD,
                0x09,
                sessionID[0], sessionID[1], sessionID[2], sessionID[3]
            };

            await client.SendAsync(handshakeDatagram, handshakeDatagram.Length);

            var handshakeResponseTask = client.ReceiveAsync();
            using var handshakeResponseCt = new CancellationTokenSource(timeout);

            if (await Task.WhenAny(handshakeResponseTask, Task.Delay(timeout, handshakeResponseCt.Token)) == handshakeResponseTask)
            {
                handshakeResponseCt.Cancel();
            }
            else
            {
                return null;
            }

            var handshakeResponseDatagram = await handshakeResponseTask;
            return new ArraySegment<byte>(handshakeResponseDatagram.Buffer, 5, handshakeResponseDatagram.Buffer.Length - 5).ToArray();
        }
    }
}
