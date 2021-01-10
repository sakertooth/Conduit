using Conduit.Minecraft;
using NetTools;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
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
                new Option<bool>(new string[] {"-q", "--query"}, () => false, "Query servers if the Server List Ping failed")
            };

            rootCommand.Handler = CommandHandler.Create<string, string, int, int, bool>(async (target, ports, timeout, size, query) =>
            {
                var runTime = Stopwatch.StartNew();
                Console.WriteLine($"Started Conduit on { DateTime.Now }");

                if (!IPAddressRange.TryParse(target, out var targetRange) || !PortRange.TryParse(ports, out var portRange))
                {
                    Console.WriteLine("Could not parse the IP or port range");
                    return;
                }

                var endpointBatchBlock = new BatchBlock<IPEndPoint>(size, new GroupingDataflowBlockOptions() { BoundedCapacity = size });

                var scanBlock = new ActionBlock<IPEndPoint[]>(async endpoints =>
                {
                    var scanTasks = new List<Task>(endpoints.Length);

                    foreach (var endpoint in endpoints)
                    {
                        scanTasks.Add(ServerListPing.SendMinecraftSlp(endpoint.Address, endpoint.Port, timeout, query).ContinueWith(t =>
                        {
                            if (t.Result != null)
                            {
                                t.Result.Print();
                                ++Found;
                            }
                        }));
                    }

                    await Task.WhenAll(scanTasks);
                }, new ExecutionDataflowBlockOptions() { BoundedCapacity = 1 });

                endpointBatchBlock.LinkTo(scanBlock, new DataflowLinkOptions() { PropagateCompletion = true });

                foreach (var ip in targetRange)
                {
                    foreach (var port in portRange)
                    {
                        await endpointBatchBlock.SendAsync(new IPEndPoint(ip, port));
                    }
                }

                endpointBatchBlock.Complete();
                await scanBlock.Completion;

                Console.WriteLine($"Found {Found} servers in {(double)runTime.ElapsedMilliseconds / 1000} seconds");
            });

            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
