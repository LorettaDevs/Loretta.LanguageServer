using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;

namespace Loretta.LanguageServer
{
    internal class VariableByNameComparer : IEqualityComparer<IVariable>
    {
        public static readonly VariableByNameComparer Instance = new VariableByNameComparer();

        public bool Equals(IVariable? x, IVariable? y) =>
            string.Equals(x?.Name, y?.Name, StringComparison.Ordinal);

        public int GetHashCode([DisallowNull] IVariable obj) =>
            obj?.Name.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}
