using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Lambda.Token;

public struct FilePosition : IEquatable<FilePosition> {
  // 0-indexed line number
  public int Line;
  // 0-indexed position within the line
  public int Column;
  // 0-indexed character offset since the start of the file
  public int Offset;

  public readonly bool Equals(FilePosition other) {
    return Offset == other.Offset && Line == other.Line &&
           Column == other.Column;
  }

  public static bool operator ==(FilePosition left, FilePosition right) {
    return left.Equals(right);
  }

  public static bool operator !=(FilePosition left, FilePosition right) {
    return !left.Equals(right);
  }

  public override readonly bool Equals(object obj) {
    return obj is FilePosition position && Equals(position);
  }

  public override readonly int GetHashCode() {
    return Offset ^ (Line << 8) ^ (Column << 16);
  }

  public readonly bool IsLessThanOrEqual(int line, int column) {
    if (Line < line) {
      return true;
    }
    if (Line > line) {
      return false;
    }
    return Column <= column;
  }

  public bool IsLessThan(int line, int column) {
    if (Line < line) {
      return true;
    }
    if (Line > line) {
      return false;
    }
    return Column < column;
  }

  public bool IsGreaterThanOrEqual(int line, int column) {
    return !IsLessThan(line, column);
  }
}

public struct Range {
  public FilePosition Start;
  public Token Token;
}

public class CoqEmitter {
  private readonly Dictionary<Token, string> _tokenNames = new();
  private readonly HashSet<string> _usedNames = new();
  private int _indent = 0;
  private FilePosition _pos = new();
  private readonly List<Range> _ranges = new();

  private readonly StreamWriter _writer;

  public CoqEmitter(Stream stream) {
    _writer = new StreamWriter(new BlockStreamFlush(stream));
    _ranges.Add(new Range() {
      Start = _pos,
      Token = null,
    });
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
    _pos.Column += _indent;
    _pos.Offset += _indent;
  }

  public void WriteNewline() {
    StartRange(null);
    _writer.Write('\n');
    _writer.Flush();
    _pos.Line++;
    _pos.Column = 0;
    _pos.Offset++;
    WriteIndent();
  }

  public void Write(char c, Token owner) {
    StartRange(owner);
    _writer.Write(c);
    _writer.Flush();
    if (c == '\n') {
      _pos.Line++;
      _pos.Column = 0;
    } else {
      _pos.Column++;
    }
    _pos.Offset++;
  }

  public void Write(string s, Token owner) {
    StartRange(owner);
    _writer.Write(s);
    _writer.Flush();

    int lineStart = 0;
    int pos = 0;
    while ((pos = s.IndexOf('\n', pos)) != -1) {
      pos++;
      lineStart = pos;
      _pos.Line++;
      _pos.Column = 0;
    }
    _pos.Column += s.Length - lineStart;
    _pos.Offset += s.Length;
  }

  private void StartRange(Token owner) {
    Range last = _ranges[^1];
    if (owner == last.Token) {
      return;
    }
    if (last.Start == _pos) {
      last.Token = owner;
      _ranges[^1] = last;
      return;
    }
    _ranges.Add(new Range() { Start = _pos, Token = owner });
  }

  // Returns all tokens that overlap the range of the generated content.
  // The line and column indices are 0-indexed.
  public HashSet<Token> FindOverlapping(int startLine, int startCol,
                                        int endLine, int endCol) {
    // Perform a binary search to get the start of the range. startLower will
    // point to the last range that is less than or equal to (startLine,
    // startCol), or 0 if the first range is greater than (startLine, startCol).
    int startLower = 0;
    int upper = _ranges.Count;
    while (startLower + 1 < upper) {
      int mid = ((upper - startLower) >> 1) + startLower;
      if (_ranges[mid].Start.IsLessThanOrEqual(startLine, startCol)) {
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
      if (_ranges[mid].Start.IsLessThan(endLine, endCol)) {
        lower = mid;
      } else {
        endUpper = mid;
      }
    }

    HashSet<Token> result = new();
    for (int i = startLower; i < endUpper; ++i) {
      if (_ranges[i].Token != null) {
        Debug.Assert(
            _ranges[i].Start.IsGreaterThanOrEqual(startLine, startCol) &&
            _ranges[i].Start.IsLessThan(endLine, endCol));
        result.Add(_ranges[i].Token);
      }
    }
    return result;
  }
}
