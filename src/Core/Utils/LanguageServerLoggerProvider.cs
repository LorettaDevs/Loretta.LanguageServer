using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Loretta.LanguageServer
{
    class LanguageServerLoggerProvider : ILoggerProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private LanguageServerLogger? _logger;

        public LanguageServerLoggerProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ILogger CreateLogger(string categoryName)
        {
            if (_logger is null)
            {
                Interlocked.CompareExchange(ref _logger, new LanguageServerLogger(_serviceProvider), null);
            }
            return _logger;
        }

        public void Dispose()
        {
        }
    }
}