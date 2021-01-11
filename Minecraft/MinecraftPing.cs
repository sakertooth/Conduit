using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Minecraft
{
    static class MinecraftPing
    {
        public static async Task<byte[]> GetResponse(IPAddress address, int port, int timeout)
        {
            using var client = new TcpClient();

            using var connectCt = new CancellationTokenSource(timeout);
            await client.ConnectAsync(address, port, connectCt.Token);
            using var stream = client.GetStream();

            using var writeCt = new CancellationTokenSource(timeout);
            await stream.WriteAsync(CreateHandshake(address, (ushort)port));

            using var readCt = new CancellationTokenSource(timeout);
            await stream.ReadVarIntAsync(readCt.Token);
            await stream.ReadVarIntAsync();

            var json = new byte[await stream.ReadVarIntAsync()];
            var jsonRead = 0;

            while (jsonRead != json.Length)
            {
                jsonRead += await stream.ReadAsync(json, jsonRead, json.Length - jsonRead);
            }

            return json;
        }
        public static async Task<MinecraftResponse> ParseResponse(IPEndPoint endpoint, byte[] json)
        {
            using var jsonStream = new MemoryStream(json);
            using var jsonDocument = await JsonDocument.ParseAsync(jsonStream);

            var root = jsonDocument.RootElement;
            var version = root.GetProperty("version").GetProperty("name").GetString();
            var players = root.GetProperty("players");
            var online = players.GetProperty("online").GetInt32();
            var max = players.GetProperty("max").GetInt32();
            var description = root.GetProperty("description");

            string responseDescription = null;
            switch (description.ValueKind)
            {
                case JsonValueKind.Object:
                    responseDescription = description.GetProperty("text").GetString();
                    break;
                case JsonValueKind.String:
                    responseDescription = description.GetString();
                    break;
            }
            return new MinecraftResponse(endpoint.Address, endpoint.Port, version, online, max, responseDescription);
        }

        static byte[] CreateHandshake(IPAddress address, ushort port)
        {
            var hostname = address.ToString();
            var buffer = new byte[9 + hostname.Length];
            var marker = 0;

            buffer[marker++] = (byte)(buffer.Length - 3);
            buffer[marker++] = 0x00;
            buffer[marker++] = 0x6E;
            buffer[marker++] = (byte)hostname.Length;
            
            for (int i = 0; i < hostname.Length; i++)
            {
                buffer[marker++] = (byte)hostname[i];
            }

            buffer[marker++] = (byte)(port << 8);
            buffer[marker++] = (byte)(port >> 8);
            buffer[marker++] = 0x01;
            buffer[marker++] = 0x01;
            buffer[marker++] = 0x00;

            return buffer;
        }

        static async Task<int> ReadVarIntAsync(this NetworkStream networkStream, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }

            int numRead = 0;
            int result = 0;
            byte read;
            do
            {
                read = await networkStream.ReadByteAsync(token);
                int value = (read & 0b01111111);
                result |= (value << (7 * numRead));

                numRead++;
                if (numRead > 5)
                {
                    throw new OverflowException("VarInt was too large");
                }
            } while ((read & 0b10000000) != 0);

            return result;
        }

        static async Task<byte> ReadByteAsync(this NetworkStream networkStream, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }

            byte[] b = new byte[1];
            await networkStream.ReadAsync(b, 0, 1, token);
            return b[0];
        }
    }
}
