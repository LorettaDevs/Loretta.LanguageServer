using System;

namespace Loretta.LanguageServer
{
    class NullDisposable : IDisposable
    {
        public static NullDisposable Singleton { get; } = new NullDisposable();
        public void Dispose() { return; }
    }
}