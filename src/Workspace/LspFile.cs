using System;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loretta.LanguageServer.Workspace
{
    /// <summary>
    /// A snapshot of a file in the workspace.
    /// </summary>
    internal readonly struct LspFile
    {
        /// <summary>
        /// Whether this file was created without any data.
        /// </summary>
        public bool IsDefault =>
            DocumentUri is null
            || Script is null
            || SyntaxTree is null;

        /// <summary>
        /// The URI to the document.
        /// </summary>
        public DocumentUri DocumentUri { get; }

        /// <summary>
        /// The script the file belongs to.
        /// </summary>
        public Script Script { get; }

        /// <summary>
        /// The file's syntax tree.
        /// </summary>
        public SyntaxTree SyntaxTree { get; }

        /// <summary>
        /// The <see cref="LuaParseOptions"/> passed when the file was parsed.
        /// </summary>
        public LuaParseOptions ParseOptions => (LuaParseOptions) SyntaxTree.Options;

        /// <summary>
        /// The file's <see cref="SourceText"/>.
        /// </summary>
        public SourceText Text => SyntaxTree.GetText();

        /// <summary>
        /// The root node for the file's <see cref="SyntaxTree"/>.
        /// </summary>
        public SyntaxNode RootNode => SyntaxTree.GetRoot();

        /// <summary>
        /// DO NOT USE. WILL ALWAYS THROW.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown because this should not be used.
        /// </exception>
        [Obsolete("Do not create an empty LspFile.", error: true)]
        public LspFile()
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Creates a new language server file.
        /// </summary>
        /// <param name="documentUri"><inheritdoc cref="DocumentUri" path="/summary"/></param>
        /// <param name="script"><inheritdoc cref="Script" path="/summary"/></param>
        /// <param name="syntaxTree"><inheritdoc cref="SyntaxTree" path="/summary"/></param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of the provided parameters is <see langword="null"/>.
        /// </exception>
        public LspFile(
            DocumentUri documentUri,
            Script script,
            SyntaxTree syntaxTree)
        {
            DocumentUri = documentUri ?? throw new ArgumentNullException(nameof(documentUri));
            Script = script ?? throw new ArgumentNullException(nameof(script));
            SyntaxTree = syntaxTree ?? throw new ArgumentNullException(nameof(syntaxTree));
        }

        /// <summary>
        /// Obtains the token at the provided <paramref name="position"/>.
        /// </summary>
        /// <param name="position">The position to obtain the token at.</param>
        /// <returns></returns>
        public SyntaxToken GetTokenAtPosition(Position position)
        {
            var pos = Text.Lines.GetPosition(position.ToLinePosition());
            return RootNode.FindToken(pos);
        }

        /// <summary>
        /// Obtains the token at the provided <paramref name="position"/>.
        /// </summary>
        /// <param name="position">The position to obtain the node at.</param>
        /// <returns></returns>
        public SyntaxNode GetNodeAtLocation(Position position) =>
            GetTokenAtPosition(position).Parent!;
    }
}
