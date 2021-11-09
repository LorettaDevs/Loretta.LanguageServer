using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Text;
using Loretta.Utilities;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Loretta.LanguageServer.Workspace
{
    internal delegate void LspFileUpdatedHandler(LspFileContainer sender, LspFile oldFile, LspFile newFile);
    internal delegate void LspFileRemovedHandler(LspFileContainer sender, LspFile file);

    internal class LspFileContainer
    {
        private class File
        {
            public DocumentUri DocumentUri { get; }
            public SyntaxTree SyntaxTree { get; set; }

            public File(DocumentUri documentUri, SyntaxTree syntaxTree)
            {
                DocumentUri = documentUri;
                SyntaxTree = syntaxTree;
            }
        }

        private readonly ReaderWriterLockSlim _lock = new();
        private readonly Dictionary<DocumentUri, File> _files = new();
        private readonly ILogger<LspFileContainer> _logger;
        private Script _script = Script.Empty;

        public LuaParseOptions ParseOptions { get; set; } = LuaParseOptions.Default;

        public IEnumerable<LspFile> GetOpenFiles()
        {
            _lock.EnterReadLock();
            try
            {
                return _files.Values.Select(GetLspFile).ToImmutableArray();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public event LspFileUpdatedHandler? FileUpdated;
        public event LspFileRemovedHandler? FileRemoved;

        public LspFileContainer(ILogger<LspFileContainer> logger) =>
            _logger = logger;

        private void OnFileUpdated(LspFile oldFile, LspFile newFile) => FileUpdated?.Invoke(this, oldFile, newFile);

        private void OnFileRemoved(LspFile file) => FileRemoved?.Invoke(this, file);

        private LspFile GetLspFile(File file)
        {
            if (file is null) throw new ArgumentNullException(nameof(file));
            Debug.Assert(_lock.IsReadLockHeld || _lock.IsUpgradeableReadLockHeld, "This method reads data therefore a read lock is required.");
            return new LspFile(file.DocumentUri, _script, file.SyntaxTree);
        }

        /// <summary>
        /// Attempts to get an open file with the provided <paramref name="documentUri"/>
        /// or reads and parses it from the filesystem if it's not open.
        /// </summary>
        /// <param name="documentUri"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="documentUri"/> is null.
        /// </exception>
        public LspFile GetOrReadFile(DocumentUri documentUri)
        {
            if (documentUri is null) throw new ArgumentNullException(nameof(documentUri));

            _lock.EnterUpgradeableReadLock();
            try
            {
                if (!_files.TryGetValue(documentUri, out var file))
                {
                    using (var stream = System.IO.File.OpenRead(documentUri.GetFileSystemPath()))
                    {
                        var text = SourceText.From(stream);
                        var syntaxTree = LuaSyntaxTree.ParseText(text, ParseOptions, documentUri.ToString());
                        file = new File(documentUri, syntaxTree);

                        _lock.EnterWriteLock();
                        try
                        {
                            _files[documentUri] = file;
                            _script = new Script(_script.SyntaxTrees.Add(syntaxTree));
                        }
                        finally
                        {
                            _lock.ExitWriteLock();
                        }
                    }

                    _logger.LogDebug("File read from disk: {documentUri}", documentUri);
                }

                return GetLspFile(file);
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Attempts to get an open file with the provided <paramref name="documentUri"/>.
        /// </summary>
        /// <param name="documentUri"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="documentUri"/> is null.
        /// </exception>
        public bool TryGetFile(DocumentUri documentUri, out LspFile file)
        {
            if (documentUri is null) throw new ArgumentNullException(nameof(documentUri));
            if (documentUri.Scheme is not ("file" or "untitled" or "vscode-notebook-cell"))
            {
                file = default;
                return false;
            }

            try
            {
                file = GetOrReadFile(documentUri);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading file at {documentUri}", documentUri);
                file = default;
                return false;
            }
        }

        /// <summary>
        /// Gets or adds an in-memory file with the provided <paramref name="documentUri"/>
        /// or loads it if the provided <paramref name="contents"/> are not <see langword="null"/>.
        /// </summary>
        /// <param name="documentUri"></param>
        /// <param name="contents"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        [return: NotNullIfNotNull("contents")]
        public LspFile? GetOrAddFile(DocumentUri documentUri, string? contents = null)
        {
            if (documentUri is null) throw new ArgumentNullException(nameof(documentUri));

            _lock.EnterUpgradeableReadLock();
            try
            {
                if (!_files.TryGetValue(documentUri, out var file) && contents is not null)
                {
                    var text = SourceText.From(contents);
                    var syntaxTree = LuaSyntaxTree.ParseText(text, ParseOptions, documentUri.ToString());
                    file = new File(documentUri, syntaxTree);

                    _lock.EnterWriteLock();
                    try
                    {
                        _files[documentUri] = file;
                        _script = new Script(_script.SyntaxTrees.Add(syntaxTree));
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }

                    _logger.LogDebug("File created from in-memory contents: {documentUri}", documentUri);
                }

                return file is null ? null : GetLspFile(file);
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Updates a file in the workspace with the provided <paramref name="newContents"/> as its contents.
        /// </summary>
        /// <param name="oldFile"></param>
        /// <param name="newContents"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="LspFile.IsDefault"/> is true for <paramref name="oldFile"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="newContents"/> is <see langword="null"/>.
        /// </exception>
        public LspFile UpdateFile(LspFile oldFile, string newContents)
        {
            if (newContents is null) throw new ArgumentNullException(nameof(newContents));
            var text = SourceText.From(newContents);
            return UpdateFile(oldFile, text);
        }

        /// <summary>
        /// Updates a file in the workspace with the provided <paramref name="textChanges"/>.
        /// </summary>
        /// <param name="oldFile"></param>
        /// <param name="textChanges"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="LspFile.IsDefault"/> is true for <paramref name="oldFile"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="textChanges"/> is <see langword="null"/>.
        /// </exception>
        public LspFile UpdateFile(LspFile oldFile, IEnumerable<TextChange> textChanges)
        {
            if (textChanges is null) throw new ArgumentNullException(nameof(textChanges));
            var text = oldFile.Text.WithChanges(textChanges);
            return UpdateFile(oldFile, text);
        }

        /// <summary>
        /// Updates a file in the workspace with the provided <paramref name="text"/> as its contents.
        /// </summary>
        /// <param name="oldFile"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="LspFile.IsDefault"/> is true for <paramref name="oldFile"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="text"/> is <see langword="null"/>.
        /// </exception>
        public LspFile UpdateFile(LspFile oldFile, SourceText text)
        {
            AssertValidFile(oldFile, nameof(oldFile));
            if (text is null) throw new ArgumentNullException(nameof(text));
            if (oldFile.Text.ContentEquals(text))
                return oldFile;

            var syntaxTree = oldFile.SyntaxTree.WithChangedText(text);
            File file;
            _lock.EnterWriteLock();
            try
            {
                file = _files[oldFile.DocumentUri];
                file.SyntaxTree = syntaxTree;
                _script = new Script(_script.SyntaxTrees.Replace(oldFile.SyntaxTree, syntaxTree));
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            var newFile = GetLspFile(file);
            OnFileUpdated(oldFile, newFile);
            return newFile;
        }

        public void RemoveFile(DocumentUri documentUri)
        {
            if (documentUri is null) throw new ArgumentNullException(nameof(documentUri));

            _lock.EnterWriteLock();
            try
            {
                if (_files.Remove(documentUri, out var file))
                {
                    var lspFile = GetLspFile(file); // Capture the state before modifying it.
                    _script = new Script(_files.Values.Select(f => f.SyntaxTree).ToImmutableArray());
                    OnFileRemoved(lspFile);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public LspFile? FindFile(SyntaxNode node)
        {
            var tree = node.SyntaxTree;
            var uri = DocumentUri.From(tree.FilePath);
            return GetOrAddFile(uri);
        }

        private static void AssertValidFile(LspFile file, string paramName)
        {
            if (file.IsDefault)
                throw new ArgumentException($"'{paramName}' must be a valid LSP file.", paramName);
        }
    }
}
