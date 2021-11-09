using System;
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

namespace Loretta.LanguageServer.Handlers
{
    internal class LuaTextDocumentSyncHandler : TextDocumentSyncHandlerBase
    {
        private static readonly Uri s_discardUri = new Uri("untitled:discard");
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
            if (request.TextDocument.LanguageId != LspConstants.LanguageId)
                return Unit.Task;

            _logger.LogTrace("Opening document {documentUri}.", request.TextDocument.Uri);
            var file = _files.GetOrAddFile(request.TextDocument.Uri, request.TextDocument.Text).Value;
            PublishDiagnostics(file);
            _logger.LogTrace("Finished opening document.");
            return Unit.Task;
        }

        public override Task<Unit> Handle(
            DidChangeTextDocumentParams request,
            CancellationToken cancellationToken)
        {
            if (_files.TryGetFile(request.TextDocument.Uri, out var file))
            {
                file = _files.UpdateFile(
                    file,
                    request.ContentChanges.Select(change => change.ToTextChange(file.Text)));
                PublishDiagnostics(file);
            }
            else
            {
                _logger.LogWarning("Received text document update for unknown document: {documentUri}", request.TextDocument.Uri);
            }
            return Unit.Task;
        }

        public override Task<Unit> Handle(
            DidSaveTextDocumentParams request,
            CancellationToken cancellationToken)
        {
            if (request.Text is null)
            {
                _logger.LogWarning("Received text document save without contents for document {documentUri}", request.TextDocument.Uri);
                return Unit.Task;
            }

            if (_files.TryGetFile(request.TextDocument.Uri, out var file))
            {
                file = _files.UpdateFile(file, request.Text);
                PublishDiagnostics(file);
            }
            else
            {
                _logger.LogWarning("Received text document update for unknown document: {documentUri}", request.TextDocument.Uri);
            }
            return Unit.Task;
        }

        public override Task<Unit> Handle(
            DidCloseTextDocumentParams request,
            CancellationToken cancellationToken)
        {
            _files.RemoveFile(request.TextDocument.Uri);
            _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = request.TextDocument.Uri,
                Diagnostics = new Container<Diagnostic>()
            });
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
