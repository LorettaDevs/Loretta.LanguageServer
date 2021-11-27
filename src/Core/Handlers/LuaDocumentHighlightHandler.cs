using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

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
            PositionHandlerHelpers.GetEverything(
                _files,
                request.TextDocument.Uri,
                request.Position,
                out var file,
                out _,
                out var token,
                out var parent);
            var script = file.Script;

            if (script.GetVariable(parent) is IVariable variable)
            {
                var highlights = new List<DocumentHighlight>();
                // Iteration and parameter variables don't have a write on their declaration
                // as the write is implicit, so we add a write highlight to them on their
                // declaration.
                if (variable.Kind is VariableKind.Iteration or VariableKind.Parameter
                    && variable.Declaration is SyntaxNode declaration)
                {
                    highlights.Add(new DocumentHighlight
                    {
                        Kind = DocumentHighlightKind.Write,
                        Range = declaration.Span.ToRange(file.Text)
                    });
                }
                foreach (var read in variable.ReadLocations)
                {
                    if (file.SyntaxTree == read.SyntaxTree)
                    {
                        highlights.Add(new DocumentHighlight
                        {
                            Kind = DocumentHighlightKind.Read,
                            Range = read.Span.ToRange(file.Text)
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
                                Range = nameNode.Span.ToRange(file.Text)
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
                        Range = label.LabelSyntax.Span.ToRange(file.Text)
                    });
                }
                foreach (var jump in label.JumpSyntaxes)
                {
                    highlights.Add(new DocumentHighlight
                    {
                        Kind = DocumentHighlightKind.Text,
                        Range = jump.Span.ToRange(file.Text)
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

            static DocumentHighlight kwHighlight(LspFile file, SyntaxToken keyword) =>
                new DocumentHighlight
                {
                    Kind = DocumentHighlightKind.Text,
                    Range = keyword.Span.ToRange(file.Text),
                };
        }
    }
}
