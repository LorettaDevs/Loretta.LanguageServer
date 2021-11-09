using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Text;
using Loretta.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Loretta.LanguageServer.Handlers
{
    internal class LuaDocumentHighlightHandler : DocumentHighlightHandlerBase
    {
        private readonly ILogger<LuaDocumentHighlightHandler> _logger;
        private readonly LspFileContainer _files;

        public LuaDocumentHighlightHandler(
            ILogger<LuaDocumentHighlightHandler> logger,
            LspFileContainer files)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _files = files ?? throw new System.ArgumentNullException(nameof(files));
        }

        protected override DocumentHighlightRegistrationOptions CreateRegistrationOptions(
            DocumentHighlightCapability capability,
            ClientCapabilities clientCapabilities)
        {
            return new DocumentHighlightRegistrationOptions
            {
                DocumentSelector = LspConstants.DocumentSelector
            };
        }

        public override Task<DocumentHighlightContainer?> Handle(
            DocumentHighlightParams request,
            CancellationToken cancellationToken)
        {
            var file = _files.GetOrReadFile(request.TextDocument.Uri);
            var token = file.GetTokenAtPosition(request.Position);
            var parent = token.Parent;
            var script = file.Script;

            // How is parent null? I don't know, but we need to check this case.
            if (parent is null)
                throw new System.Exception("Token parent is null???");

            if (script.GetVariable(parent) is IVariable variable)
            {
                var highlights = new List<DocumentHighlight>();
                foreach (var read in variable.ReadLocations)
                {
                    if (file.SyntaxTree == read.SyntaxTree)
                    {
                        highlights.Add(new DocumentHighlight
                        {
                            Kind = DocumentHighlightKind.Read,
                            Range = toRange(file, read.Span)
                        });
                    }
                }
                foreach (var write in variable.WriteLocations)
                {
                    if (file.SyntaxTree == write.SyntaxTree)
                    {
                        var assigneFindResult = AssignmentHelpers.GetVariableAssigneeNodeInAssignment(variable, write);
                        if (assigneFindResult is { IsSome: true, Value: SyntaxNode nameNode })
                        {
                            highlights.Add(new DocumentHighlight
                            {
                                Kind = DocumentHighlightKind.Write,
                                Range = toRange(file, nameNode.Span)
                            });
                        }
                    }
                }
                return Task.FromResult<DocumentHighlightContainer?>(
                    DocumentHighlightContainer.From(highlights));
            }
            else if (script.GetLabel(parent) is IGotoLabel label)
            {
                var highlights = new List<DocumentHighlight>();
                if (label.LabelSyntax is not null)
                {
                    highlights.Add(new DocumentHighlight
                    {
                        Kind = DocumentHighlightKind.Text,
                        Range = toRange(file, label.LabelSyntax.Span)
                    });
                }
                foreach (var jump in label.JumpSyntaxes)
                {
                    highlights.Add(new DocumentHighlight
                    {
                        Kind = DocumentHighlightKind.Text,
                        Range = toRange(file, jump.Span)
                    });
                }
                return Task.FromResult<DocumentHighlightContainer?>(
                    DocumentHighlightContainer.From(highlights));
            }
            // Highlight the other keywords of statements
            else if (token.IsKeyword() && parent.Kind().ToString().EndsWith("Statement", System.StringComparison.Ordinal))
            {
                var child = parent.ChildTokens();
                var keywords = child.Where(static token => token.IsKeyword());
                return Task.FromResult<DocumentHighlightContainer?>(
                    DocumentHighlightContainer.From(keywords.Select(token => kwHighlight(file, token))));
            }

            // Else return null.
            return Task.FromResult<DocumentHighlightContainer?>(null);

            static Range toRange(LspFile file, TextSpan span) =>
                file.Text.Lines.GetLinePositionSpan(span).ToRange();
            static DocumentHighlight kwHighlight(LspFile file, SyntaxToken keyword) =>
                new DocumentHighlight
                {
                    Kind = DocumentHighlightKind.Text,
                    Range = toRange(file, keyword.Span),
                };
        }
    }
}
