using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Lua.Syntax;
using Tsu;

namespace Loretta.LanguageServer
{
    internal class AssignmentHelpers
    {
        public static Option<SyntaxNode> GetVariableAssigneeNodeInAssignment(IVariable variable, SyntaxNode assignment)
        {
            switch (assignment.Kind())
            {
                case SyntaxKind.LocalVariableDeclarationStatement:
                {
                    var localVarDecl = (LocalVariableDeclarationStatementSyntax) assignment;
                    return getFromList(variable, localVarDecl.Names);
                }
                case SyntaxKind.AssignmentStatement:
                {
                    var assignmentStatement = (AssignmentStatementSyntax) assignment;
                    return getFromList(variable, assignmentStatement.Variables);
                }
                case SyntaxKind.LocalFunctionDeclarationStatement:
                {
                    var localFunctionStatement = (LocalFunctionDeclarationStatementSyntax) assignment;
                    return localFunctionStatement.Name;
                }
                case SyntaxKind.FunctionDeclarationStatement:
                {
                    var functionDeclarationStatement = (FunctionDeclarationStatementSyntax) assignment;
                    return functionDeclarationStatement.Name;
                }
                default:
                {
                    if (SyntaxFacts.IsCompoundAssignmentStatement(assignment.Kind()))
                    {
                        var compoundAssignmentStatement = (CompoundAssignmentStatementSyntax) assignment;
                        return compoundAssignmentStatement.Variable;
                    }
                    else
                    {
                        throw new NotSupportedException($"Finding the variable in an assignment of type {assignment.Kind()} is not supported.");
                    }
                }
            }

            static Option<SyntaxNode> getFromList(IVariable variable, IEnumerable<PrefixExpressionSyntax> variables)
            {
                var variablesArr = variables.ToImmutableArray();
                var idx = GetIndexOfVariableInList(variable, variablesArr);
                if (idx == -1)
                    return Option.None<SyntaxNode>();
                else
                    return variablesArr[idx];
            }
        }

        public static Option<SyntaxNode> GetVariableValueInAssignment(IVariable variable, SyntaxNode assignment)
        {
            switch (assignment.Kind())
            {
                case SyntaxKind.LocalVariableDeclarationStatement:
                {
                    var localVarDecl = (LocalVariableDeclarationStatementSyntax) assignment;
                    var valuesArr = localVarDecl.Values.ToImmutableArray();
                    var idx = GetIndexOfVariableInList(variable, localVarDecl.Names);
                    if (idx < valuesArr.Length)
                        return valuesArr[idx];
                    else
                        return Option.None<SyntaxNode>();
                }

                case SyntaxKind.AssignmentStatement:
                {
                    var assignmentStatement = (AssignmentStatementSyntax) assignment;
                    var valuesArr = assignmentStatement.Values.ToImmutableArray();
                    var idx = GetIndexOfVariableInList(variable, assignmentStatement.Variables);
                    if (idx < valuesArr.Length)
                        return valuesArr[idx];
                    else
                        return Option.None<SyntaxNode>();
                }

                case SyntaxKind.LocalFunctionDeclarationStatement:
                case SyntaxKind.FunctionDeclarationStatement:
                    return assignment;

                default:
                {
                    if (SyntaxFacts.IsCompoundAssignmentStatement(assignment.Kind()))
                    {
                        var compoundAssignmentStatement = (CompoundAssignmentStatementSyntax) assignment;
                        return compoundAssignmentStatement.Expression;
                    }
                    else
                    {
                        throw new NotSupportedException($"Finding the variable in an assignment of type {assignment.Kind()} is not supported.");
                    }
                }
            }
        }

        private static int GetIndexOfVariableInList(IVariable variable, IEnumerable<PrefixExpressionSyntax> variables)
        {
            var idx = 0; var found = -1;
            foreach (var variableSyntax in variables)
            {
                if (variableSyntax is IdentifierNameSyntax identifierName
                    && variable.Name.Equals(identifierName.Name, StringComparison.Ordinal))
                {
                    found = idx;
                }

                idx++;
            }
            return found;
        }
    }
}
