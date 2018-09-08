using LanguageServer;
using LanguageServer.Parameters;
using LanguageServer.Parameters.General;
using LanguageServer.Parameters.TextDocument;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SampleServer
{
    public class TextDocumentManager
    {
        private readonly Dictionary<Uri, TextDocument> _documents;

        public TextDocumentManager()
        {
            _documents = new Dictionary<Uri, TextDocument>();
        }

        public TextDocumentSyncKind SyncKind => TextDocumentSyncKind.Full;

        public event EventHandler<TextDocumentChangedEventArgs> DidChangeContent;
        public event EventHandler<TextDocumentChangedEventArgs> DidOpen;
        public event EventHandler<TextDocumentWillSaveEventArgs> WillSave;
        public event RequestHandler<TextDocumentWillSaveEventArgs, TextEdit[], ResponseError> WillSaveWaitUntil;
        public event EventHandler<TextDocumentChangedEventArgs> DidSave;
        public event EventHandler<TextDocumentChangedEventArgs> DidClose;

        protected virtual void OnDidChangeContent(TextDocumentChangedEventArgs args)
        {
            DidChangeContent?.Invoke(this, args);
        }

        protected virtual void OnDidOpen(TextDocumentChangedEventArgs args)
        {
            DidOpen?.Invoke(this, args);
        }

        protected virtual void OnWillSave(TextDocumentWillSaveEventArgs args)
        {
            WillSave?.Invoke(this, args);
        }

        protected virtual Result<TextEdit[], ResponseError> OnWillSaveWaitUntil(TextDocumentWillSaveEventArgs args, CancellationToken token)
        {
            return WillSaveWaitUntil?.Invoke(this, args, token);
        }

        protected virtual void OnDidSave(TextDocumentChangedEventArgs args)
        {
            DidSave?.Invoke(this, args);
        }

        protected virtual void OnDidClose(TextDocumentChangedEventArgs args)
        {
            DidClose?.Invoke(this, args);
        }


        public TextDocument this[Uri uri] => _documents[uri];

        public IReadOnlyCollection<TextDocument> All => _documents.Values;

        public IReadOnlyCollection<Uri> Keys => _documents.Keys;

        public void Listen(Connection connection)
        {
            // connection.__textDocumentSync = TextDocumentSyncKind.Full;
            connection.NotificationHandlers.Set<NotificationMessage<DidOpenTextDocumentParams>>("textDocument/didOpen", DidOpenTextDocument);
            connection.NotificationHandlers.Set<NotificationMessage<DidChangeTextDocumentParams>>("textDocument/didChange", DidChangeTextDocument);
            connection.NotificationHandlers.Set<NotificationMessage<DidCloseTextDocumentParams>>("textDocument/didClose", DidCloseTextDocument);
            connection.NotificationHandlers.Set<NotificationMessage<WillSaveTextDocumentParams>>("textDocument/willSave", WillSaveTextDocument);
            connection.RequestHandlers.Set<RequestMessage<WillSaveTextDocumentParams>, ResponseMessage<TextEdit[], ResponseError>>("textDocument/willSaveWaitUntil", WillSaveWaitUntilTextDocument);
            connection.NotificationHandlers.Set<NotificationMessage<DidSaveTextDocumentParams>>("textDocument/didSave", DidSaveTextDocument);
        }

        private void DidOpenTextDocument(NotificationMessage<DidOpenTextDocumentParams> message)
        {
            var td = message.@params.textDocument;
            var document = new TextDocument(td.uri, td.languageId, td.version, td.text);
            _documents[td.uri] = document;
            var toFire = new TextDocumentChangedEventArgs(document);
            OnDidOpen(toFire);
            OnDidChangeContent(toFire);
        }

        private void DidChangeTextDocument(NotificationMessage<DidChangeTextDocumentParams> message)
        {
            var td = message.@params.textDocument;
            var changes = message.@params.contentChanges;
            var last = changes?.Length > 0 ? changes[changes.Length - 1] : null;
            if (last != null)
            {
                if (_documents.TryGetValue(td.uri, out var document))
                {
                    document.Update(last, td.version);
                    OnDidChangeContent(new TextDocumentChangedEventArgs(document));
                }
            }
        }

        private void DidCloseTextDocument(NotificationMessage<DidCloseTextDocumentParams> message)
        {
            var td = message.@params.textDocument;
            if (_documents.TryGetValue(td.uri, out var document))
            {
                _documents.Remove(td.uri);
                OnDidClose(new TextDocumentChangedEventArgs(document));
            }
        }

        private void WillSaveTextDocument(NotificationMessage<WillSaveTextDocumentParams> message)
        {
            var td = message.@params.textDocument;
            var reason = message.@params.reason;
            if (_documents.TryGetValue(td.uri, out var document))
            {
                OnWillSave(new TextDocumentWillSaveEventArgs(document, reason));
            }
        }

        private ResponseMessage<TextEdit[], ResponseError> WillSaveWaitUntilTextDocument(RequestMessage<WillSaveTextDocumentParams> message, CancellationToken token)
        {
            var td = message.@params.textDocument;
            var reason = message.@params.reason;
            var result = default(Result<TextEdit[], ResponseError>);
            if (_documents.TryGetValue(td.uri, out var document))
            {
                result = OnWillSaveWaitUntil(new TextDocumentWillSaveEventArgs(document, reason), token);
            }
            if (result == null)
            {
                result = Result<TextEdit[], ResponseError>.Success(new TextEdit[] { });
            }
            return new ResponseMessage<TextEdit[], ResponseError>
            {
                id = message.id,
                result = result.SuccessValue,
                error = result.ErrorValue
            };
        }

        private void DidSaveTextDocument(NotificationMessage<DidSaveTextDocumentParams> message)
        {
            var td = message.@params.textDocument;
            if (_documents.TryGetValue(td.uri, out var document))
            {
                OnDidSave(new TextDocumentChangedEventArgs(document));
            }
        }
    }
}
