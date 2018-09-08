using LanguageServer.Parameters.TextDocument;
using System;

namespace SampleServer
{
    public class TextDocumentWillSaveEventArgs : EventArgs
    {
        public TextDocumentWillSaveEventArgs(TextDocument document, TextDocumentSaveReason reason)
        {
            Document = document;
            Reason = reason;
        }

        public TextDocument Document { get; }

        public TextDocumentSaveReason Reason { get; }
    }
}
