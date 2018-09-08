using System;

namespace SampleServer
{
    public class TextDocumentChangedEventArgs : EventArgs
    {
        public TextDocumentChangedEventArgs(TextDocument document)
        {
            Document = document;
        }

        public TextDocument Document { get; }
    }
}
