using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loretta.LanguageServer
{
    internal static class LspConstants
    {
        public static readonly string LanguageId = "lua";
        public static readonly string LanguageFileExtension = ".lua";
        public static readonly DocumentSelector DocumentSelector = DocumentSelector.ForLanguage(LanguageId);

        // Standard library hack
        // This is REALLY BAD and I am NOT pround of it.
        #region Standard Library

        public static readonly IImmutableSet<string> StandardLibraryFunctions = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            // Functions
            "getmetatable",
            "ipairs",
            "next",
            "pairs",
            "select",
            "setmetatable",
            "tonumber",
            "tostring",
            "type",
            "print",
            "assert",
            "pcall",
            "xpcall",
            "error",
            "collectgarbage",
            "require");

        public static readonly IImmutableDictionary<string, IImmutableSet<string>> StandardLibraryTypes = ImmutableDictionary.CreateRange(
            StringComparer.Ordinal,
            new[]
            {
                CreateType("bit"),
                CreateType("string"),
                CreateType("math"),
                CreateType("table"),
                CreateType("coroutine"),
                CreateType("io"),
                CreateType("debug"),
            });

        private static KeyValuePair<string, IImmutableSet<string>> CreateType(string name, IEnumerable<string>? members = null)
        {
            return new KeyValuePair<string, IImmutableSet<string>>(
                name,
                members != null
                ? ImmutableHashSet.CreateRange(StringComparer.Ordinal, members)
                : ImmutableHashSet<string>.Empty);
        }

        #endregion Standard Library
    }
}
