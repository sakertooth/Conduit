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
                    Console.WriteLine("Could not parse the IP or port range.");
                    return;
                }

                var endpointJoinBlock = new JoinBlock<IPAddress, int>();

                var endpointBatchBlock = new BatchBlock<Tuple<IPAddress, int>>(size, new GroupingDataflowBlockOptions() { EnsureOrdered = false });

                var scanBlock = new ActionBlock<Tuple<IPAddress, int>[]>(async endpoints =>
                {
                    var scanTasks = new List<Task>(endpoints.Length);

                    foreach (var endpoint in endpoints)
                    {
                        var (addr, port) = endpoint;
                        scanTasks.Add(ServerListPing.SendMinecraftSlp(addr, port, timeout, query).ContinueWith(t =>
                        {
                            if (t.Result != null)
                            {
                                t.Result.Print();
                                ++Found;
                            }
                        }));
                    }

                    await Task.WhenAll(scanTasks);
                });

                var linkOptions = new DataflowLinkOptions() { PropagateCompletion = true };
                endpointJoinBlock.LinkTo(endpointBatchBlock, linkOptions);
                endpointBatchBlock.LinkTo(scanBlock, linkOptions);

                foreach (var ip in targetRange)
                {
                    foreach (var port in portRange)
                    {
                        endpointJoinBlock.Target1.Post(ip);
                        endpointJoinBlock.Target2.Post(port);
                    }
                }

                endpointJoinBlock.Complete();
                await scanBlock.Completion;

                Console.WriteLine($"{ Found } servers were found in {(double)runTime.ElapsedMilliseconds / 1000} seconds");
            });

            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
