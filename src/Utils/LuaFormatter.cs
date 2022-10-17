using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Text;
using Loretta.LanguageServer.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Loretta.LanguageServer
{
    internal static class LuaFormatter
    {
        public static IEnumerable<TextChange> GetFormatTextEdits(
            FormattingOptions options,
            LspFile file,
            Range? range = null)
        {
            var indentation = options.InsertSpaces ? new string(' ', options.TabSize) : "\t";
            var eol = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "\r\n" : "\n";

            SyntaxNode node;
            SyntaxTree tree;
            TextSpan? formatSpan = null;
            if (range == null)
            {
                node = file.RootNode;
                tree = file.SyntaxTree;
            }
            else
            {
                formatSpan = range.ToTextSpan(file.Text);
                node = file.RootNode.FindNode(formatSpan.Value);
                tree = file.SyntaxTree.WithRootAndOptions(node, file.ParseOptions);
            }

            var formattedRoot = node.NormalizeWhitespace(indentation: indentation, eol: eol);
            var formattedTree = tree.WithRootAndOptions(formattedRoot, file.ParseOptions);
            var changes = formattedTree.GetChanges(tree);

            if (formatSpan.HasValue)
                return changes.Where(change => formatSpan.Value.Contains(change.Span));
            else
                return changes;
        }
    }
}
