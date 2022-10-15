using System;
using System.Globalization;
using System.Linq;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LSP = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LCA = Loretta.CodeAnalysis;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Loretta.LanguageServer
{
    internal static class LspExtensions
    {
        public static LinePosition ToLinePosition(this Position position) =>
            new LinePosition(position.Line, position.Character);

        public static Position ToPosition(this LinePosition position) =>
            new Position(position.Line, position.Character);

        public static LinePositionSpan ToLinePositionSpan(this Range range) =>
            new LinePositionSpan(range.Start.ToLinePosition(), range.End.ToLinePosition());

        public static Range ToRange(this LinePositionSpan positionSpan) =>
            new Range(positionSpan.Start.ToPosition(), positionSpan.End.ToPosition());

        public static TextSpan ToTextSpan(this Range range, SourceText text) =>
            text.Lines.GetTextSpan(range.ToLinePositionSpan());

        public static Range ToRange(this TextSpan span, SourceText text) =>
            text.Lines.GetLinePositionSpan(span).ToRange();

        public static TextChange ToTextChange(this TextDocumentContentChangeEvent change, SourceText text) =>
            new TextChange(change.Range!.ToTextSpan(text), change.Text);

        public static TextEdit ToTextEdit(this TextChange textChange, SourceText sourceText) =>
            new TextEdit
            {
                Range = textChange.Span.ToRange(sourceText),
                NewText = textChange.NewText!,
            };

        public static LSP.DiagnosticSeverity ToLspSeverity(this LCA.DiagnosticSeverity severity) =>
            severity switch
            {
                LCA.DiagnosticSeverity.Hidden => LSP.DiagnosticSeverity.Hint,
                LCA.DiagnosticSeverity.Info => LSP.DiagnosticSeverity.Information,
                LCA.DiagnosticSeverity.Warning => LSP.DiagnosticSeverity.Warning,
                LCA.DiagnosticSeverity.Error => LSP.DiagnosticSeverity.Error,
                _ => throw new ArgumentException("Invalid diagnostic severity.", nameof(severity)),
            };

        public static LSP.Diagnostic ToLspDiagnostic(this LCA.Diagnostic diagnostic, DocumentUri documentUri) =>
            new LSP.Diagnostic
            {
                Severity = diagnostic.Severity.ToLspSeverity(),
                Code = diagnostic.Id,
                Message = diagnostic.GetMessage(CultureInfo.CurrentCulture),
                Range = new Range
                {
                    Start = diagnostic.Location.GetLineSpan().StartLinePosition.ToPosition(),
                    End = diagnostic.Location.GetLineSpan().EndLinePosition.ToPosition(),
                },
                RelatedInformation = Container.From(diagnostic.AdditionalLocations.Select(add =>
                    new DiagnosticRelatedInformation
                    {
                        Location = new LSP.Location
                        {
                            Uri = documentUri,
                            Range = new Range
                            {
                                Start = add.GetLineSpan().StartLinePosition.ToPosition(),
                                End = add.GetLineSpan().EndLinePosition.ToPosition(),
                            }
                        }
                    })),
            };

        public static Range GetRange(this SyntaxToken token, SourceText text) =>
            token.Span.ToRange(text);

        public static Range GetRange(this SyntaxNode token, SourceText text) =>
            token.Span.ToRange(text);
    }
}
