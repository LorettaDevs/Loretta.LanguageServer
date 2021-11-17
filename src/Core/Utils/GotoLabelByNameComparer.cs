using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;

namespace Loretta.LanguageServer.Utils
{
    internal class GotoLabelByNameComparer : IEqualityComparer<IGotoLabel>
    {
        public static readonly GotoLabelByNameComparer Instance = new GotoLabelByNameComparer();

        public bool Equals(IGotoLabel? x, IGotoLabel? y) =>
            string.Equals(x?.Name, y?.Name, StringComparison.Ordinal);

        public int GetHashCode([DisallowNull] IGotoLabel obj) =>
            obj?.Name.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}
