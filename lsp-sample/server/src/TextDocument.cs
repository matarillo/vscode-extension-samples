using LanguageServer.Parameters;
using LanguageServer.Parameters.TextDocument;
using System;
using System.Collections.Generic;

namespace SampleServer
{
    public class TextDocument
    {
        private IReadOnlyList<int> _lineOffsets;

        public TextDocument(Uri uri, string languageId, long version, string content)
        {
            this.Uri = uri;
            this.LanguageId = languageId;
            this.Version = version;
            this.Text = content;
            this._lineOffsets = null;
        }

        public Uri Uri { get; }

        public string LanguageId { get; }

        public long Version { get; private set; }

        public string Text { get; private set; }

        public string GetText(Range range)
        {
            if (range != null)
            {
                var start = OffsetAt(range.start);
                var end = OffsetAt(range.end);
                return Text.Substring(start, end - start + 1);
            }
            return Text;
        }

        public void Update(TextDocumentContentChangeEvent @event, long version)
        {
            this.Text = @event.text;
            this.Version = version;
            this._lineOffsets = null;
        }

        private IReadOnlyList<int> LineOffsets
        {
            get
            {
                if (this._lineOffsets == null)
                {
                    var lineOffsets = new List<int>();
                    var text = Text;
                    var isLineStart = true;
                    for (var i = 0; i < text.Length; i++)
                    {
                        if (isLineStart)
                        {
                            lineOffsets.Add(i);
                            isLineStart = false;
                        }
                        var ch = text[i];
                        isLineStart = (ch == '\r' || ch == '\n');
                        if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        {
                            i++;
                        }
                    }
                    if (isLineStart && text.Length > 0)
                    {
                        lineOffsets.Add(text.Length);
                    }
                    this._lineOffsets = lineOffsets;
                }
                return this._lineOffsets;
            }
        }

        public Position PositionAt(int offset)
        {
            offset = Math.Max(Math.Min(offset, Text.Length), 0);
            var lineOffsets = LineOffsets;
            var low = 0;
            var high = lineOffsets.Count;
            if (high == 0)
            {
                return new Position { line = 0, character = offset };
            }
            while (low < high)
            {
                var mid = (low + high) / 2;
                if (lineOffsets[mid] > offset)
                {
                    high = mid;
                }
                else
                {
                    low = mid + 1;
                }
            }
            // low is the least x for which the line offset is larger than the current offset
            // or array.length if no line offset is larger than the current offset
            var line = low - 1;
            return new Position { line = line, character = offset - lineOffsets[line] };
        }

        public int OffsetAt(Position position)
        {
            var lineOffsets = LineOffsets;
            if (position.line >= lineOffsets.Count)
            {
                return Text.Length;
            }
            else if (position.line < 0)
            {
                return 0;
            }
            var line = (int)position.line;
            var character = (int)position.character;
            var lineOffset = lineOffsets[line];
            var nextLineOffset = (position.line + 1 < lineOffsets.Count) ? lineOffsets[line + 1] : Text.Length;
            return Math.Max(Math.Min(lineOffset + character, nextLineOffset), lineOffset);
        }

        public int LineCount => LineOffsets.Count;
    }
}