using System.IO;
using System.Threading.Tasks;

namespace Loretta.LanguageServer
{
    using System;
    using System.IO.Pipelines;
    using System.Threading;
    using Loretta.LanguageServer.Handlers;
    using Loretta.LanguageServer.Workspace;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using OmniSharp.Extensions.LanguageServer.Server;
    using Serilog;

    public class LuaLanguageServer
    {
        private readonly LanguageServer _languageServer;

        public LuaLanguageServer(Stream input, Stream output)
            : this(opts => opts.WithInput(input).WithOutput(output))
        {
        }

        public LuaLanguageServer(PipeReader input, PipeWriter output)
            : this(opts => opts.WithInput(input).WithOutput(output))
        {
        }

        private LuaLanguageServer(Action<LanguageServerOptions> optionsInit)
        {
            _languageServer = LanguageServer.PreInit(options =>
            {
                options.ConfigureLogging(builder =>
                {
                    builder.AddLanguageProtocolLogging();
                    builder.AddSerilog(Log.Logger);
#if DEBUG
                    builder.SetMinimumLevel(LogLevel.Trace);
#else
                    builder.SetMinimumLevel(LogLevel.Information);
#endif
                });
                options.WithHandler<LuaTextDocumentSyncHandler>()
                       .WithHandler<LuaDefinitionHandler>()
                       .WithHandler<LuaDocumentHighlightHandler>()
                       .WithHandler<LuaDocumentFormattingHandler>()
                       .WithHandler<LuaDocumentRangeFormattingHandler>()
                       .WithHandler<LuaCompletionHandler>()
                       .WithHandler<LuaSemanticTokensHandler>()
                       // TODO: DocumentSymbolHandler
                       .WithHandler<LuaReferencesHandler>()
                       // TODO: RenameHandler
                       // TODO: HoverHandler
                       // TODO: CodeActionsHandler
                       // TODO: DidChangeWatchedFilesHandler
                       // TODO: SignatureHelpHandler
                       .WithServices(RegisterServices);

                optionsInit(options);
            });
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await _languageServer.Initialize(cancellationToken);
            await _languageServer.WaitForExit;
        }

        private void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<LspFileContainer>();
            services.AddLogging(cfg =>
            {
#if DEBUG
                cfg.SetMinimumLevel(LogLevel.Trace);
#else
                cfg.SetMinimumLevel(LogLevel.Information);
#endif
            });
        }
    }
}
