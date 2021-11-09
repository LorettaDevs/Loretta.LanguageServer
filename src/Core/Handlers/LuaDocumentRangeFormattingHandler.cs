using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loretta.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loretta.LanguageServer.Handlers
{
    internal class LuaDocumentRangeFormattingHandler : DocumentRangeFormattingHandlerBase
    {
        private readonly ILogger<LuaDocumentFormattingHandler> _logger;
        private readonly LspFileContainer _files;

        public LuaDocumentRangeFormattingHandler(
            ILogger<LuaDocumentFormattingHandler> logger,
            LspFileContainer files)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _files = files ?? throw new System.ArgumentNullException(nameof(files));
        }

        protected override DocumentRangeFormattingRegistrationOptions CreateRegistrationOptions(DocumentRangeFormattingCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentRangeFormattingRegistrationOptions
            {
                DocumentSelector = LspConstants.DocumentSelector,
            };
        }

        public override Task<TextEditContainer> Handle(DocumentRangeFormattingParams request, CancellationToken cancellationToken)
        {
            _logger.LogTrace("Received format request for {documentUri}", request.TextDocument.Uri);
            var file = _files.GetOrReadFile(request.TextDocument.Uri);
            var changes = LuaFormatter.GetFormatTextEdits(request.Options, file, request.Range);

            return Task.FromResult(TextEditContainer.From(changes.Select(change => change.ToTextEdit(file.Text))));
        }
    }
}
