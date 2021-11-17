using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Loretta.LanguageServer
{
    internal static partial class Log
    {
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Opening document '{DocumentUri}' with language ID: {LanguageId}")]
        public static partial void LogDocumentOpenReceived(
            this ILogger logger,
            DocumentUri documentUri,
            string languageId);

        [LoggerMessage(
            Level = LogLevel.Trace,
            Message = "Finished updating document in {TotalTime} (Workspace: {ParseTime} | Diagnostic Push: {DiagnosticsPushTime}).")]
        public static partial void LogFinishedUpdatingDocument(
            this ILogger logger,
            string totalTime,
            string parseTime,
            string diagnosticsPushTime);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Unknown language ID {LanguageId} received")]
        public static partial void LogUnknownLanguageIdReceived(
            this ILogger logger,
            string languageId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Received text document change for document '{DocumentUri}'")]
        public static partial void LogDocumentChangeReceived(
            this ILogger logger,
            DocumentUri documentUri);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Received text document update for unknown document '{DocumentUri}'")]
        public static partial void LogTextChangeForUnknownDocumentReceived(
            this ILogger logger,
            DocumentUri documentUri);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Received text document save for document '{DocumentUri}'")]
        public static partial void LogDocumentSaveReceived(
            this ILogger logger,
            DocumentUri documentUri);

        [LoggerMessage(
            Level = LogLevel.Trace,
            Message = "Received text document save without contents for document '{DocumentUri}'")]
        public static partial void LogTextlessSaveReceived(
            this ILogger logger,
            DocumentUri documentUri);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Received text document close for document '{DocumentUri}'")]
        public static partial void LogDocumentCloseReceived(
            this ILogger logger,
            DocumentUri documentUri);
    }
}
