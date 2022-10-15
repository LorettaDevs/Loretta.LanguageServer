using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loretta.LanguageServer.Workspace;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Tsu.Numerics;

namespace Loretta.LanguageServer.Handlers
{
    internal class LuaTextDocumentSyncHandler : TextDocumentSyncHandlerBase
    {
        private readonly ILogger<LuaTextDocumentSyncHandler> _logger;
        private readonly LspFileContainer _files;
        private readonly ILanguageServerFacade _languageServer;

        public LuaTextDocumentSyncHandler(
            ILogger<LuaTextDocumentSyncHandler> logger,
            LspFileContainer files,
            ILanguageServerFacade languageServer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
        }

        protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
            SynchronizationCapability capability,
            ClientCapabilities clientCapabilities)
        {
            return new TextDocumentSyncRegistrationOptions
            {
                DocumentSelector = LspConstants.DocumentSelector,
                Change = TextDocumentSyncKind.Incremental,
                Save = false
            };
        }

        public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
            new TextDocumentAttributes(uri, LspConstants.LanguageId);

        public override Task<Unit> Handle(
            DidOpenTextDocumentParams request,
            CancellationToken cancellationToken)
        {
            _logger.LogDocumentOpenReceived(
                request.TextDocument.Uri,
                request.TextDocument.LanguageId);
            if (request.TextDocument.LanguageId != LspConstants.LanguageId)
            {
                _logger.LogUnknownLanguageIdReceived(request.TextDocument.LanguageId);
                return Unit.Task;
            }

            var start = Stopwatch.GetTimestamp();
            var file = _files.GetOrAddFile(request.TextDocument.Uri, request.TextDocument.Text).Value;
            var addEnd = Stopwatch.GetTimestamp();
            PublishDiagnostics(file);
            var diagnosticsPushEnd = Stopwatch.GetTimestamp();
            _logger.LogFinishedUpdatingDocument(
                Duration.Format(diagnosticsPushEnd - start),
                Duration.Format(addEnd - start),
                Duration.Format(diagnosticsPushEnd - addEnd));
            return Unit.Task;
        }

        public override Task<Unit> Handle(
            DidChangeTextDocumentParams request,
            CancellationToken cancellationToken)
        {
            _logger.LogDocumentChangeReceived(request.TextDocument.Uri);
            if (_files.TryGetFile(request.TextDocument.Uri, out var file))
            {
                var start = Stopwatch.GetTimestamp();
                file = _files.UpdateFile(
                    file,
                    request.ContentChanges.Select(change => change.ToTextChange(file.Text)));
                var updateEnd = Stopwatch.GetTimestamp();
                PublishDiagnostics(file);
                var diagnosticsPushEnd = Stopwatch.GetTimestamp();
                _logger.LogFinishedUpdatingDocument(
                    Duration.Format(diagnosticsPushEnd - start),
                    Duration.Format(updateEnd - start),
                    Duration.Format(diagnosticsPushEnd - updateEnd));
            }
            else
            {
                _logger.LogTextChangeForUnknownDocumentReceived(request.TextDocument.Uri);
            }
            return Unit.Task;
        }

        public override Task<Unit> Handle(
            DidSaveTextDocumentParams request,
            CancellationToken cancellationToken)
        {
            _logger.LogDocumentSaveReceived(request.TextDocument.Uri);
            if (request.Text is null)
            {
                _logger.LogTextChangeForUnknownDocumentReceived(request.TextDocument.Uri);
                return Unit.Task;
            }

            if (_files.TryGetFile(request.TextDocument.Uri, out var file))
            {
                var start = Stopwatch.GetTimestamp();
                file = _files.UpdateFile(file, request.Text);
                var updateEnd = Stopwatch.GetTimestamp();
                PublishDiagnostics(file);
                var diagnosticsPushEnd = Stopwatch.GetTimestamp();
                _logger.LogFinishedUpdatingDocument(
                    Duration.Format(diagnosticsPushEnd - start),
                    Duration.Format(updateEnd - start),
                    Duration.Format(diagnosticsPushEnd - updateEnd));
            }
            else
            {
                _logger.LogTextChangeForUnknownDocumentReceived(request.TextDocument.Uri);
            }
            return Unit.Task;
        }

        public override Task<Unit> Handle(
            DidCloseTextDocumentParams request,
            CancellationToken cancellationToken)
        {
            _logger.LogDocumentCloseReceived(request.TextDocument.Uri);

            var start = Stopwatch.GetTimestamp();
            _files.RemoveFile(request.TextDocument.Uri);
            var removeEnd = Stopwatch.GetTimestamp();
            _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = request.TextDocument.Uri,
                Diagnostics = new Container<Diagnostic>()
            });
            var diagnosticsPushEnd = Stopwatch.GetTimestamp();
            _logger.LogFinishedUpdatingDocument(
                Duration.Format(diagnosticsPushEnd - start),
                Duration.Format(removeEnd - start),
                Duration.Format(diagnosticsPushEnd - removeEnd));

            return Unit.Task;
        }

        private void PublishDiagnostics(LspFile file)
        {
            _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = file.DocumentUri,
                Diagnostics = Container<Diagnostic>.From(
                    file.SyntaxTree.GetDiagnostics()
                                   .Select(diag => diag.ToLspDiagnostic(file.DocumentUri)))
            });
        }
    }
}
