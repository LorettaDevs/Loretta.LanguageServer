using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loretta.LanguageServer
{
    internal static class LspConstants
    {
        public static readonly string LanguageId = "lua";
        public static readonly string LanguageFileExtension = ".lua";
        public static readonly DocumentSelector DocumentSelector = DocumentSelector.ForLanguage(LanguageId);
    }
}
