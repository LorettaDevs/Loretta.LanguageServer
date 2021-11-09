using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Loretta.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loretta.LanguageServer.Handlers
{
    internal partial class LuaSemanticTokensHandler : SemanticTokensHandlerBase
    {
        private readonly ILogger<LuaSemanticTokensHandler> _logger;
        private readonly LspFileContainer _files;
        private readonly Dictionary<DocumentUri, SemanticTokensDocument> _documents;

        public LuaSemanticTokensHandler(ILogger<LuaSemanticTokensHandler> logger, LspFileContainer files)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _documents = new Dictionary<DocumentUri, SemanticTokensDocument>();
            _files = files ?? throw new System.ArgumentNullException(nameof(files));
            _files.FileRemoved += WorkspaceFileRemoved;
        }

        private void WorkspaceFileRemoved(LspFileContainer sender, LspFile file) =>
            _documents.Remove(file.DocumentUri);

        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
            SemanticTokensCapability capability,
            ClientCapabilities clientCapabilities)
        {
            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = LspConstants.DocumentSelector,
                Legend = new(),
                Full = new SemanticTokensCapabilityRequestFull
                {
                    Delta = true
                },
                Range = true
            };
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
            ITextDocumentIdentifierParams @params,
            CancellationToken cancellationToken)
        {
            if (!_documents.TryGetValue(@params.TextDocument.Uri, out var document))
            {
                document = new SemanticTokensDocument(RegistrationOptions);
                _documents.Add(@params.TextDocument.Uri, document);
            }
            return Task.FromResult(document);
        }

        protected override Task Tokenize(
            SemanticTokensBuilder builder,
            ITextDocumentIdentifierParams identifier,
            CancellationToken cancellationToken)
        {
            var file = _files.GetOrReadFile(identifier.TextDocument.Uri);
            SemanticTokenVisitor.Tokenize(file.Script, file.Text, builder, file.RootNode, cancellationToken);
            return Task.CompletedTask;
        }
    }
}
