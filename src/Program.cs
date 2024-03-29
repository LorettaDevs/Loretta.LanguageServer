﻿using System;
using System.IO;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

// This class is based on bicep's language server's Program
namespace Loretta.LanguageServer
{
    internal class Program
    {
        public static async Task Main()
        {
            var profilePath = Path.GetTempPath();
            ProfileOptimization.SetProfileRoot(profilePath);
            ProfileOptimization.StartProfile("lorettalsp.profile");

            await RunWithCancellationAsync(async cancellationToken =>
            {
                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.File(Path.Combine(Path.GetTempPath(), "loretta-lsp.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 5)
#if DEBUG
                    .MinimumLevel.Verbose()
#else
                    .MinimumLevel.Warning()
#endif
                    .CreateLogger();

                // the server uses JSON-RPC over stdin & stdout to communicate,
                // so be careful not to use console for logging!
                var server = new LuaLanguageServer(
                                Console.OpenStandardInput(),
                                Console.OpenStandardOutput());

                await server.RunAsync(cancellationToken);
            });
        }

        private static async Task RunWithCancellationAsync(Func<CancellationToken, Task> runFunc)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                cancellationTokenSource.Cancel();
                e.Cancel = true;
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                cancellationTokenSource.Cancel();
            };

            try
            {
                await runFunc(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
