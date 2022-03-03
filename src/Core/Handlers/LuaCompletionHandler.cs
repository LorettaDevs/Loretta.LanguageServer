using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
using Tsu.Numerics;

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
            _logger.LogCompletionRequestReceived(request.TextDocument.Uri, request.Position);
            PositionHandlerHelpers.GetEverything(
                _files,
                request.TextDocument.Uri,
                request.Position,
                out var file,
                out var position,
                out var token,
                out var parent);
            var previousToken = token.GetPreviousToken();

            // We also don't provide completion for properties, methods, function names or local declarations.
            if (parent.Span.Contains(position)
                && parent is MemberAccessExpressionSyntax
                          or MethodCallExpressionSyntax
                          or FunctionNameSyntax
                          or LocalFunctionDeclarationStatementSyntax
                          or LocalVariableDeclarationStatementSyntax)
            {
                _logger.LogCannotProvideCompletionForToken();
                goto fail;
            }

            var scope = file.Script.FindScope(parent);
            if (scope is null)
            {
                _logger.LogHighlightedTokenIsNotInAnyScopes();
                return Task.FromResult(new CompletionList());
            }

            // If we're in the goto token but outside of its span, it means we're in trivia.
            if (previousToken.IsKind(SyntaxKind.GotoKeyword) || token.IsKind(SyntaxKind.GotoKeyword))
            {
                _logger.LogGeneratingGotoCompletions();
                SyntaxToken? partialToken = previousToken.IsKind(SyntaxKind.GotoKeyword) ? token : null;
                var start = Stopwatch.GetTimestamp();
                var items = CompletionList.From(GenerateGotoCandidates(file, partialToken, scope, cancellationToken));
                var end = Stopwatch.GetTimestamp();
                _logger.LogCompletionsGenerated(Duration.Format(end - start));
                return Task.FromResult(items);
            }
            else
            {
                _logger.LogGeneratingVariableCompletions();
                SyntaxToken? partialToken = token.Span.Contains(position) ? token : null;
                var start = Stopwatch.GetTimestamp();
                var items = CompletionList.From(GenerateVariableCandidates(file, partialToken, scope, cancellationToken));
                var end = Stopwatch.GetTimestamp();
                _logger.LogCompletionsGenerated(Duration.Format(end - start));
                return Task.FromResult(items);
            }

            throw new InvalidOperationException("Unreacheable.");

        fail:
            _logger.LogFailedToGenerateCompletions();
            return Task.FromResult(CompletionList.From())!;
        }

        private static IEnumerable<CompletionItem> GenerateGotoCandidates(
            LspFile file,
            SyntaxToken? partialToken,
            IScope? scope,
            CancellationToken cancellationToken)
        {
            var candidates = new HashSet<IGotoLabel>(GotoLabelByNameComparer.Instance);
            for (var currentScope = scope; currentScope != null; currentScope = currentScope.ContainingScope)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var label in currentScope.GotoLabels)
                {
                    if (partialToken != null && !label.Name.StartsWith(partialToken.Value.Text, StringComparison.Ordinal))
                        continue;
                    candidates.Add(label);
                }
            }

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (partialToken != null)
                {
                    yield return new CompletionItem
                    {
                        Label = candidate.Name,
                        Kind = CompletionItemKind.Reference,
                        Preselect = candidates.Count == 1,
                        TextEdit = new TextEdit
                        {
                            Range = partialToken!.Value.GetRange(file.Text),
                            NewText = candidate.Name,
                        },
                    };
                }
                else
                {
                    yield return new CompletionItem
                    {
                        Label = candidate.Name,
                        Kind = CompletionItemKind.Reference,
                        Preselect = candidates.Count == 1,
                        InsertText = candidate.Name,
                    };
                }
            }
        }

        private static IEnumerable<CompletionItem> GenerateVariableCandidates(
            LspFile file,
            SyntaxToken? partialToken,
            IScope? scope,
            CancellationToken cancellationToken)
        {
            var candidates = new HashSet<IVariable>(VariableByNameComparer.Instance);
            for (var currentScope = scope; currentScope != null; currentScope = currentScope.ContainingScope)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var variable in currentScope.DeclaredVariables)
                {
                    if (partialToken != null && !variable.Name.StartsWith(partialToken.Value.Text, StringComparison.Ordinal))
                        continue;
                    candidates.Add(variable);
                }
            }

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var writes = candidate.WriteLocations.ToImmutableArray();
                if (partialToken != null)
                {
                    yield return new CompletionItem
                    {
                        Label = candidate.Name,
                        Kind = writes.Length == 1
                             && writes[0] is LocalFunctionDeclarationStatementSyntax
                                                or FunctionDeclarationStatementSyntax
                             ? CompletionItemKind.Function
                             : CompletionItemKind.Variable,
                        Preselect = candidates.Count == 1,
                        TextEdit = new TextEdit
                        {
                            Range = partialToken.Value.GetRange(file.Text),
                            NewText = candidate.Name,
                        },
                    };
                }
                else
                {
                    yield return new CompletionItem
                    {
                        Label = candidate.Name,
                        Kind = writes.Length == 1
                             && writes[0] is LocalFunctionDeclarationStatementSyntax
                                                or FunctionDeclarationStatementSyntax
                             ? CompletionItemKind.Function
                             : CompletionItemKind.Variable,
                        Preselect = candidates.Count == 1,
                        InsertText = candidate.Name,
                    };
                }
            }
        }

        // In VSCode this is sent to get more details about the highlighted completion item
        public override Task<CompletionItem> Handle(
            CompletionItem request,
            CancellationToken cancellationToken) =>
            // We currently don't support getting more details for a completion.
            throw new NotSupportedException();
    }
}
