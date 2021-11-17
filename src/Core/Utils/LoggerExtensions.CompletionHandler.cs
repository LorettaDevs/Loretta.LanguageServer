using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Loretta.LanguageServer
{
    internal static partial class LoggerExtensions
    {
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Completion request received for {DocumentUri} at position {Position}")]
        public static partial void LogCompletionRequestReceived(
            this ILogger logger,
            DocumentUri documentUri,
            Position position);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Highlighted token is part of a property, method, function name or local declaration")]
        public static partial void LogCannotProvideCompletionForToken(this ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Highlighted token is not contained in any scopes?")]
        public static partial void LogHighlightedTokenIsNotInAnyScopes(this ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Generating goto completions...")]
        public static partial void LogGeneratingGotoCompletions(this ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Generating variable completions...")]
        public static partial void LogGeneratingVariableCompletions(this ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Trace,
            Message = "Completions generated in {Duration}")]
        public static partial void LogCompletionsGenerated(
            this ILogger logger,
            string duration);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Failed to generate completion list.")]
        public static partial void LogFailedToGenerateCompletions(this ILogger logger);
    }
}
