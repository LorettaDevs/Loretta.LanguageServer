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
    internal class LuaDefinitionHandler : DefinitionHandlerBase
    {
        private readonly LspFileContainer _files;

        public LuaDefinitionHandler(LspFileContainer workspaceService) =>
            _files = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));

        protected override DefinitionRegistrationOptions CreateRegistrationOptions(
            DefinitionCapability capability,
            ClientCapabilities clientCapabilities)
        {
            return new DefinitionRegistrationOptions
            {
                DocumentSelector = LspConstants.DocumentSelector,
            };
        }

        public override Task<LocationOrLocationLinks> Handle(
            DefinitionParams request,
            CancellationToken cancellationToken)
        {
            PositionHandlerHelpers.GetEverything(
                _files,
                request.TextDocument.Uri,
                request.Position,
                out var file,
                out _,
                out _,
                out var parent);

            if (file.Script.GetVariable(parent) is IVariable variable)
            {
                if (variable.Declaration is SyntaxNode declaration)
                {
                    var definitionFile = declaration.SyntaxTree == file.SyntaxTree
                        ? file
                        : _files.FindFile(declaration);
                    if (definitionFile.HasValue)
                    {
                        return Task.FromResult(LocationOrLocationLinks.From(
                          new Location
                          {
                              Uri = definitionFile.Value.DocumentUri,
                              Range = definitionFile.Value.Text.Lines.GetLinePositionSpan(declaration.Span).ToRange(),
                          }));
                    }
                }
                else
                {
                    var locations = new List<LocationOrLocationLink>();
                    foreach (var write in variable.WriteLocations)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var writeFile = write.SyntaxTree == file.SyntaxTree
                            ? file
                            : _files.FindFile(write);
                        var assigneeFindResult = AssignmentHelpers.GetVariableAssigneeNodeInAssignment(variable, write);
                        if (writeFile is LspFile && assigneeFindResult is { IsSome: true, Value: SyntaxNode nameNode })
                        {
                            locations.Add(new Location
                            {
                                Uri = writeFile.Value.DocumentUri,
                                Range = writeFile.Value.Text.Lines.GetLinePositionSpan(nameNode.Span).ToRange(),
                            });
                        }
                    }
                    return Task.FromResult(LocationOrLocationLinks.From(locations));
                }
            }
            else if (file.Script.GetLabel(parent) is IGotoLabel label
                && label.LabelSyntax is not null)
            {
                // We don't need FindFile for this because goto labels can't span multiple files.
                return Task.FromResult(LocationOrLocationLinks.From(
                    new Location
                    {
                        Uri = file.DocumentUri,
                        Range = file.Text.Lines.GetLinePositionSpan(label.LabelSyntax.Span).ToRange()
                    }));
            }

            return Task.FromResult(new LocationOrLocationLinks());
        }
    }
}
