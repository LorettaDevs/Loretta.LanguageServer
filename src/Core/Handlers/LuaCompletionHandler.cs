using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Lua.Syntax;
using Loretta.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loretta.LanguageServer.Handlers
{
    internal class LuaCompletionHandler : CompletionHandlerBase
    {
        private readonly ILogger<LuaCompletionHandler> _logger;
        private readonly LspFileContainer _files;

        public LuaCompletionHandler(ILogger<LuaCompletionHandler> logger, LspFileContainer files)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _files = files ?? throw new ArgumentNullException(nameof(files));
        }

        protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = LspConstants.DocumentSelector,
                TriggerCharacters = Container<string>.From(".", ":"),
                ResolveProvider = false,
            };
        }

        public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var file = _files.GetOrReadFile(request.TextDocument.Uri);
            var position = file.Text.Lines.GetPosition(request.Position.ToLinePosition());
            var token = file.RootNode.FindToken(position);
            var previousToken = token.GetPreviousToken();

            if (token.Parent is not SyntaxNode parent)
                throw new Exception("Token has no parent.");

            // We don't know how to complete anything that isn't an identifier or a goto label.
            if (token.Kind() is not (SyntaxKind.IdentifierToken or SyntaxKind.GotoKeyword)
                && !previousToken.IsKind(SyntaxKind.GotoKeyword))
            {
                goto fail;
            }

            // We also don't provide completion for properties, methods, function names or local declarations.
            if (parent is MemberAccessExpressionSyntax
                       or MethodCallExpressionSyntax
                       or FunctionNameSyntax
                       or LocalFunctionDeclarationStatementSyntax
                       or LocalVariableDeclarationStatementSyntax)
            {
                goto fail;
            }

            IScope? scope = null;
            foreach (var node in parent.AncestorsAndSelf())
            {
                if (file.Script.GetScope(node) is IScope temp)
                {
                    scope = temp;
                    break;
                }
            }

            if (scope is null)
            {
                _logger.LogWarning("Highlighted token is not contained in any scopes?");
                return Task.FromResult(new CompletionList());
            }

            var list = new List<CompletionItem>();
            var originalScope = scope;

            // If we're in the goto token but outside of its span, it means we're in trivia.
            if (token.IsKind(SyntaxKind.GotoKeyword) && position > token.Span.End)
            {
                // Find a goto label.
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidates = scope.GotoLabels.ToImmutableArray();
                    foreach (var candidate in candidates)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        list.Add(new CompletionItem
                        {
                            Label = candidate.Name,
                            Kind = CompletionItemKind.Reference,
                            Preselect = candidates.Length == 1,
                            InsertText = candidate.Name,
                        });
                    }

                    scope = scope.Parent;
                }
                // We stop at the outermost function scope.
                while (scope != null && scope.Kind != ScopeKind.Function);
            }
            else if (previousToken.IsKind(SyntaxKind.GotoKeyword))
            {   
                // Find a goto label.
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidates = scope.GotoLabels
                                          .Where(label => label.Name.StartsWith(token.Text, StringComparison.Ordinal))
                                          .ToImmutableArray();
                    foreach (var candidate in candidates)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        list.Add(new CompletionItem
                        {
                            Label = candidate.Name,
                            Kind = CompletionItemKind.Reference,
                            Preselect = candidates.Length == 1,
                            TextEdit = new TextEdit
                            {
                                Range = token.GetRange(file.Text),
                                NewText = candidate.Name,
                            },
                        });
                    }

                    scope = scope.Parent;
                }
                while (scope != null);
            }
            else
            {
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidates = scope.DeclaredVariables
                                          .Where(var => var.Name.StartsWith(token.Text, StringComparison.Ordinal))
                                          .ToImmutableArray();
                    foreach (var candidate in candidates)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var writes = candidate.WriteLocations.ToImmutableArray();
                        list.Add(new CompletionItem
                        {
                            Label = candidate.Name,
                            Kind = writes.Length == 1
                                   && writes[0] is LocalFunctionDeclarationStatementSyntax
                                                      or FunctionDeclarationStatementSyntax
                                   ? CompletionItemKind.Function
                                   : CompletionItemKind.Variable,
                            Preselect = candidates.Length == 1,
                            TextEdit = new TextEdit
                            {
                                Range = token.GetRange(file.Text),
                                NewText = candidate.Name,
                            },
                        });
                    }

                    scope = scope.Parent;
                }
                while (scope != null);
            }

            return Task.FromResult(CompletionList.From(list));

        fail:
            return Task.FromResult(new CompletionList());
        }

        // In VSCode this is sent to get more details about the highlighted completion item
        public override Task<CompletionItem> Handle(
            CompletionItem request,
            CancellationToken cancellationToken) =>
            // We currently don't support getting more details for a completion.
            throw new NotSupportedException();
    }
}
