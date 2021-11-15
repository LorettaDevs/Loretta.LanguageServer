using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace Loretta.LanguageServer
{
    class LanguageServerLogger : ILogger
    {
        private readonly IServiceProvider _serviceProvider;
        private ILanguageServer? _languageServer;

        public LanguageServerLogger(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private ILanguageServer GetLanguageServer()
        {
            if (_languageServer is null)
            {
                Interlocked.CompareExchange(ref _languageServer, _serviceProvider.GetRequiredService<ILanguageServer>(), null);
            }
            return _languageServer;
        }

        public IDisposable BeginScope<TState>(TState state) => NullDisposable.Singleton;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            GetLanguageServer().LogMessage(new LogMessageParams
            {
                Type = logLevel switch
                {
                    LogLevel.Critical or LogLevel.Error => MessageType.Error,
                    LogLevel.Warning => MessageType.Warning,
                    LogLevel.Information => MessageType.Info,
                    _ => MessageType.Log,
                },
                Message = formatter(state, exception)
            });
        }
    }
}