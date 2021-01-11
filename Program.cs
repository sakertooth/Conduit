using Conduit.Minecraft;
using NetTools;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace Conduit
{
    class Program
    {
        public static int Found { get; set; }

        static int Main(string[] args)
        {
            var rootCommand = new RootCommand("A fast Minecraft server scanner in C#")
            {
                new Argument<string>("target", "Target to scan for"),
                new Argument<string>("ports", () => "25565", "Port/Port range to scan with"),
                new Option<int>(new string[] {"-t", "--timeout" }, () => 500, "Timeout in milliseconds"),
                new Option<int>(new string[] {"-s", "--size"}, () => 254, "Number of hosts to scan by batch"),
            };

            rootCommand.Handler = CommandHandler.Create<string, string, int, int, bool>(async (target, ports, timeout, size, query) =>
            {
                var runTime = Stopwatch.StartNew();
                Console.WriteLine($"Started Conduit on { DateTime.Now }");

                if (!IPAddressRange.TryParse(target, out var targetRange) || !PortRange.TryParse(ports, out var portRange))
                {
                    Console.WriteLine("Could not parse the target and/or port range");
                    return;
                }

                var blockOptions = new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = size, BoundedCapacity = size, EnsureOrdered = false };
                var linkOptions = new DataflowLinkOptions() { PropagateCompletion = true };

                var pingBlock = new TransformBlock<IPEndPoint, Tuple<IPEndPoint, byte[]>>(async endpoint =>
                {
                    try
                    {
                        return Tuple.Create(endpoint, await MinecraftPing.GetResponse(endpoint.Address, endpoint.Port, timeout));
                    }
                    catch (Exception ex) when (ex is IOException || ex is SocketException) { return null; }
                }, blockOptions);

                var parseJsonBlock = new TransformBlock<Tuple<IPEndPoint, byte[]>, MinecraftResponse>(async json =>
                {
                    try
                    {
                        var (endpoint, buffer) = json;
                        return await MinecraftPing.ParseResponse(endpoint, buffer);
                    }
                    catch (JsonException) { return null; }
                    catch (KeyNotFoundException) { return null; }
                    catch (InvalidOperationException) { return null; }
                }, blockOptions);

                var printBlock = new ActionBlock<MinecraftResponse>(response =>
                {
                    Console.WriteLine($"{response.Address}:{response.Port} [{response.Version}] ({response.Online}/{response.Max}) {response.Description}");
                    ++Found;
                }, blockOptions);

                pingBlock.LinkTo(parseJsonBlock, linkOptions, x => x != null);
                pingBlock.LinkTo(DataflowBlock.NullTarget<Tuple<IPEndPoint, byte[]>>(), linkOptions);

                parseJsonBlock.LinkTo(printBlock, linkOptions, x => x != null);
                parseJsonBlock.LinkTo(DataflowBlock.NullTarget<MinecraftResponse>());

                foreach (var ip in targetRange)
                {
                    foreach (var port in portRange)
                    {
                        await pingBlock.SendAsync(new IPEndPoint(ip, port));
                    }
                }

                pingBlock.Complete();
                await printBlock.Completion;

                Console.WriteLine($"Found {Found} servers in {(double)runTime.ElapsedMilliseconds / 1000} seconds");
            });

            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
