using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Lambda.Token;

public struct Range {
  public long Start;
  public Token Token;
}

public class CoqEmitter {
  private readonly Dictionary<Token, string> _tokenNames = new();
  private readonly HashSet<string> _usedNames = new();
  private int _indent = 0;
  // The byte offset of each line.
  private readonly List<long> _lineOffsets = new();
  private readonly List<Range> _ranges = new();

  private readonly StreamWriter _writer;

  public CoqEmitter(Stream stream) {
    _writer = new StreamWriter(new BlockStreamFlush(stream));
    _ranges.Add(new Range() {
      Start = _writer.BaseStream.Position,
      Token = null,
    });
    _lineOffsets.Add(_writer.BaseStream.Position);
  }

  public string GetName(Token t) {
    if (_tokenNames.TryGetValue(t, out string name)) {
      return name;
    }
    name = AddName(t);
    _tokenNames.Add(t, name);
    _usedNames.Add(name);
    return name;
  }

  private string AddName(Token t) {
    StringBuilder sb = new();
    t.GetPreferredIdentifier(sb);
    string attempt = sb.ToString();
    if (_usedNames.Add(attempt)) {
      return attempt;
    }
    sb.Append('_');
    int offsetStart = sb.Length;
    int offset = 0;
    do {
      ++offset;
      sb.Remove(offsetStart, sb.Length - offsetStart);
      sb.Append(offset);
      attempt = sb.ToString();
    } while (!_usedNames.Add(attempt));
    return attempt;
  }

  public void AddIndent() { AddIndent(2); }
  public void AddIndent(int count) { _indent += count; }

  public void ReleaseIndent() { ReleaseIndent(2); }
  public void ReleaseIndent(int count) { _indent -= count; }

  private void WriteIndent() {
    for (int i = 0; i < _indent; ++i) {
      _writer.Write(' ');
    }
    _writer.Flush();
  }

  public void WriteNewline() {
    StartRange(null);
    _writer.Write('\n');
    _writer.Flush();
    _lineOffsets.Add(_writer.BaseStream.Position);
    WriteIndent();
  }

  public void Write(char c, Token owner) {
    StartRange(owner);
    _writer.Write(c);
    _writer.Flush();
    if (c == '\n') {
      _lineOffsets.Add(_writer.BaseStream.Position);
    }
  }

  public void Write(string s, Token owner) {
    StartRange(owner);
    int pos = 0;
    while ((pos = s.IndexOf('\n', pos)) != -1) {
      pos++;
      _lineOffsets.Add(_writer.BaseStream.Position + pos);
    }
    _writer.Write(s);
    _writer.Flush();
  }

  private void StartRange(Token owner) {
    Range last = _ranges[^1];
    if (owner == last.Token) {
      return;
    }
    if (last.Start == _writer.BaseStream.Position) {
      last.Token = owner;
      _ranges[^1] = last;
      return;
    }
    _ranges.Add(
        new Range() { Start = _writer.BaseStream.Position, Token = owner });
  }

  // Returns the byte offset of the (line, column) location. Both line and
  // column are 0-indexed. Column is simply treated as a byte offset relative to
  // the start of the line. So if the column value goes past the end of the
  // line, then the returned offest will be on the subsequent line.
  public long ResolveLineColumn(int line, long column) {
    return _lineOffsets[line] + column;
  }

  // Returns all tokens that overlap the range of the generated content.
  public HashSet<Token> FindOverlapping(long start, long end) {
    // Perform a binary search to get the start of the range. startLower will
    // point to the last range that is less than or equal to (startLine,
    // startCol), or 0 if the first range is greater than (startLine, startCol).
    int startLower = 0;
    int upper = _ranges.Count;
    while (startLower + 1 < upper) {
      int mid = ((upper - startLower) >> 1) + startLower;
      if (_ranges[mid].Start <= start) {
        startLower = mid;
      } else {
        upper = mid;
      }
    }

    // Perform another binary search to get the end of the range. endUpper will
    // point to the first range that greater than or equal to (endLine, endCol),
    // or _ranges.Count if (endLine, endCol) is greater than the last range.
    int lower = startLower;
    int endUpper = _ranges.Count;
    while (lower + 1 < endUpper) {
      int mid = ((endUpper - lower) >> 1) + lower;
      if (_ranges[mid].Start < end) {
        lower = mid;
      } else {
        endUpper = mid;
      }
    }

    HashSet<Token> result = new();
    for (int i = startLower; i < endUpper; ++i) {
      if (_ranges[i].Token != null) {
        Debug.Assert(_ranges[i].Start >= start && _ranges[i].Start < end);
        result.Add(_ranges[i].Token);
      }
    }
    return result;
  }

  public HashSet<Token> FindOverlapping(int startLine, long startColumn,
                                        int endLine, long endColumn) {
    return FindOverlapping(ResolveLineColumn(startLine, startColumn),
                           ResolveLineColumn(endLine, endColumn));
  }
}
