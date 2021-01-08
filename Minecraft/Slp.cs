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
    public static class ServerListPing
    {
        public static async Task<MinecraftResponse> SendMinecraftSlp(IPAddress address, int port, int timeout, bool queryOnFailure)
        {
            if (address == null || port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort || timeout <= 0)
            {
                return null;
            }

            using var client = new TcpClient();
            try
            {
                //1: Connect
                using var connectCt = new CancellationTokenSource(timeout);
                await client.ConnectAsync(address, port, connectCt.Token);

                //2: Write Handshake to Server
                using var stream = client.GetStream();

                if (!stream.CanWrite)
                {
                    return null;
                }

                using var writeCt = new CancellationTokenSource(timeout);
                await stream.WriteAsync(CreateHandshakeWithRequest(address, (ushort)port), writeCt.Token);

                //3: Read Response from Server
                if (!stream.CanRead)
                {
                    return null;
                }

                using var readCt = new CancellationTokenSource(timeout);
                await stream.ReadVarIntAsync(readCt.Token);
                await stream.ReadVarIntAsync();

                var jsonLength = await stream.ReadVarIntAsync();
                if (jsonLength == 0)
                {
                    return null;
                }

                var json = new byte[jsonLength];
                var jsonBytesRead = 0;

                while (jsonBytesRead < jsonLength)
                {
                    jsonBytesRead += await stream.ReadAsync(json, jsonBytesRead, jsonLength - jsonBytesRead);
                }

                //4: Parse Response
                using var jsonStream = new MemoryStream(json);
                using var jsonDocument = await JsonDocument.ParseAsync(jsonStream);

                var root = jsonDocument.RootElement;
                if (!root.TryGetProperty("version", out var version) || !version.TryGetProperty("name", out var versionName) ||
                    !root.TryGetProperty("players", out var players) || !players.TryGetProperty("online", out var playersOnline) ||
                    !players.TryGetProperty("max", out var playersMax))
                {
                    return null;
                }

                return new MinecraftResponse(address, port, versionName.ToString(), playersOnline.GetInt32(), playersMax.GetInt32());
            }
            catch (Exception ex) when (ex is SocketException || ex is IOException || ex is InvalidOperationException || ex is JsonException || ex is OperationCanceledException)
            {
                if (!queryOnFailure)
                {
                    return null;
                }

                return await Query.SendQuery(address, port, timeout);
            }
        }

        static byte[] CreateHandshakeWithRequest(IPAddress address, ushort port)
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
