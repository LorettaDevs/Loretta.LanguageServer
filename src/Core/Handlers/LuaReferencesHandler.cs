using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.LanguageServer.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Location = OmniSharp.Extensions.LanguageServer.Protocol.Models.Location;

namespace Loretta.LanguageServer.Handlers
{
    internal class LuaReferencesHandler : ReferencesHandlerBase
    {
        private readonly LspFileContainer _files;

        public LuaReferencesHandler(LspFileContainer workspaceService) =>
            _files = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));

        protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities)
        {
            return new ReferenceRegistrationOptions
            {
                DocumentSelector = LspConstants.DocumentSelector,
            };
        }

        public override Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken)
        {
            var file = _files.GetOrReadFile(request.TextDocument.Uri);
            var position = file.Text.Lines.GetPosition(request.Position.ToLinePosition());
            var token = file.RootNode.FindToken(position);
            var parent = token.Parent;
            if (parent is null)
                throw new Exception("Token parent is null.");

            if (file.Script.GetVariable(parent) is IVariable variable)
            {
                var locations = new List<Location>();
                if (request.Context.IncludeDeclaration && variable.Declaration is SyntaxNode declaration)
                {
                    var declarationFile = GetFile(file, declaration);
                    if (declarationFile.HasValue)
                    {
                        locations.Add(new Location
                        {
                            Uri = declarationFile.Value.DocumentUri,
                            Range = declaration.Span.ToRange(declarationFile.Value.Text),
                        });
                    }
                }
                foreach (var readNode in variable.ReadLocations)
                {
                    var readFile = GetFile(file, readNode);
                    if (readFile.HasValue)
                    {
                        locations.Add(new Location
                        {
                            Uri = readFile.Value.DocumentUri,
                            Range = readNode.Span.ToRange(readFile.Value.Text)
                        });
                    }
                }
                foreach (var writeNode in variable.WriteLocations)
                {
                    var writeFile = GetFile(file, writeNode);
                    if (writeFile.HasValue)
                    {
                        locations.Add(new Location
                        {
                            Uri = writeFile.Value.DocumentUri,
                            Range = writeNode.Span.ToRange(writeFile.Value.Text)
                        });
                    }
                }
                return Task.FromResult(LocationContainer.From(locations));
            }
            else if (file.Script.GetLabel(parent) is IGotoLabel label)
            {
                var locations = new List<Location>();
                if (request.Context.IncludeDeclaration && label.LabelSyntax is SyntaxNode declaration)
                {
                    var declarationFile = GetFile(file, declaration);
                    if (declarationFile.HasValue)
                    {
                        locations.Add(new Location
                        {
                            Uri = declarationFile.Value.DocumentUri,
                            Range = declaration.Span.ToRange(declarationFile.Value.Text)
                        });
                    }
                }
                foreach (var jump in label.JumpSyntaxes)
                {
                    var jumpFile = GetFile(file, jump);
                    if (jumpFile.HasValue)
                    {
                        locations.Add(new Location
                        {
                            Uri = jumpFile.Value.DocumentUri,
                            Range = jump.Span.ToRange(jumpFile.Value.Text)
                        });
                    }
                }
                return Task.FromResult(LocationContainer.From(locations));
            }

            return Task.FromResult(new LocationContainer());
        }

        private LspFile? GetFile(LspFile currentFile, SyntaxNode node)
        {
            return node.SyntaxTree == currentFile.SyntaxTree
                ? currentFile
                : _files.FindFile(node);
        }
    }
}
