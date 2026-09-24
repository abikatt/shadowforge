using System.Text;

namespace ShadowForge.Formats.Text;

/// <summary>
/// A Shift-JIS text file as a list of lines, each keeping its own terminator.
/// Re-encoding an unmodified document reproduces the input bytes exactly, because
/// the PUA-aware Shift-JIS codec carries orphan and non-canonical byte sequences
/// through a decode and encode. Individual lines can still be rewritten.
/// </summary>
public sealed class LineDocument
{
    public sealed class Line
    {
        public string Content { get; internal set; }

        /// <summary>
        /// "\r\n", "\n", or "" for the last line.
        /// </summary>
        public string Terminator { get; }

        internal Line(string content, string terminator)
        {
            Content = content;
            Terminator = terminator;
        }
    }

    private readonly List<Line> _lines;

    private LineDocument(List<Line> lines) => _lines = lines;

    public IReadOnlyList<Line> Lines => _lines;

    public static LineDocument Parse(byte[] bytes)
    {
        string text = EncodingExtensions.DecodeShiftJISRaw(bytes, 0, bytes.Length);
        var lines = new List<Line>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                bool cr = i > start && text[i - 1] == '\r';
                int contentEnd = cr ? i - 1 : i;
                lines.Add(new Line(text[start..contentEnd], cr ? "\r\n" : "\n"));
                start = i + 1;
            }
        }

        lines.Add(new Line(text[start..], ""));
        return new LineDocument(lines);
    }

    /// <summary>
    /// Rewrites one line's content, keeping its terminator.
    /// </summary>
    public void ReplaceContent(int index, string newContent) =>
        _lines[index].Content = newContent;

    public byte[] ToBytes()
    {
        var sb = new StringBuilder();
        foreach (var line in _lines)
            sb.Append(line.Content).Append(line.Terminator);
        return sb.ToString().EncodeShiftJIS();
    }
}
