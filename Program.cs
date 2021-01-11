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
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace Conduit
{
    class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand("A fast Minecraft server scanner in C#")
            {
                new Argument<string>("target", "Target to scan for"),
                new Argument<string>("ports", () => "25565", "Port/Port range to scan with"),
                new Option<int>(new string[] {"-t", "--timeout" }, () => 500, "Timeout in milliseconds"),
                new Option<int>(new string[] {"-s", "--size"}, () => 256, "Number of hosts to scan by batch"),
            };
            
            rootCommand.Handler = CommandHandler.Create<string, string, int, int, bool>(async (target, ports, timeout, size, query) =>
            {
                if (!IPAddressRange.TryParse(target, out var targetRange) || !PortRange.TryParse(ports, out var portRange))
                {
                    Console.WriteLine("Could not parse the target and/or port range");
                    return;
                }

                var serversFound = 0;
                var blockOptions = new ExecutionDataflowBlockOptions() { BoundedCapacity = size, MaxDegreeOfParallelism = size, SingleProducerConstrained = true, EnsureOrdered = false };
                var linkOptions = new DataflowLinkOptions() { PropagateCompletion = true };

                var pingBlock = new TransformBlock<IPEndPoint, MinecraftResponse>(async endpoint =>
                {
                    try
                    {
                        return await MinecraftPing.SendAsync(endpoint.Address, endpoint.Port, timeout);
                    }
                    catch (Exception ex) when (ex is SocketException || ex is IOException || ex is OperationCanceledException || 
                                                ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException)
                    {
                        return null;
                    }
                }, blockOptions);

                var printBlock = new ActionBlock<MinecraftResponse>(response =>
                {
                    Console.WriteLine($"{response.Address}:{response.Port} [{response.Version}] ({response.Online}/{response.Max}) {response.Description}");
                    Interlocked.Increment(ref serversFound);
                }, blockOptions);

                pingBlock.LinkTo(printBlock, linkOptions, response => response != null);
                pingBlock.LinkTo(DataflowBlock.NullTarget<MinecraftResponse>(), linkOptions);

                var runTime = Stopwatch.StartNew();
                Console.WriteLine($"Started Conduit on { DateTime.Now }");

                foreach (var ip in targetRange)
                {
                    foreach (var port in portRange)
                    {
                        await pingBlock.SendAsync(new IPEndPoint(ip, port));
                    }
                }

                pingBlock.Complete();
                await printBlock.Completion;

                Console.WriteLine($"Found {serversFound} servers in {(double)runTime.ElapsedMilliseconds / 1000} seconds");
            });

            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
