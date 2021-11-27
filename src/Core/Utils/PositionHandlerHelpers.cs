using System;
using Loretta.CodeAnalysis;
using Loretta.LanguageServer.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loretta.LanguageServer
{
    internal static class PositionHandlerHelpers
    {
        public static void GetEverything(
            LspFileContainer files,
            DocumentUri documentUri,
            Position lspPosition,
            out LspFile file,
            out int position,
            out SyntaxToken token,
            out SyntaxNode parent)
        {
            file = files.GetOrReadFile(documentUri);
            position = file.Text.Lines.GetPosition(lspPosition.ToLinePosition());
            token = file.RootNode.FindToken(position);

            if (token.Parent is null)
                throw new Exception("Token has no parent.");
            parent = token.Parent;
        }
    }
}
