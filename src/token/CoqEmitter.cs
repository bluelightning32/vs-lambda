using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Lambda.Network;

namespace Lambda.Token;

public class CoqEmitter {
  private readonly Dictionary<Token, string> _tokenNames = new();
  private readonly HashSet<string> _usedNames = new();
  private int _indent = 0;

  public readonly TextWriter Writer;

  public CoqEmitter(TextWriter writer) { Writer = writer; }

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

  public void WriteIndent() {
    for (int i = 0; i < _indent; ++i) {
      Writer.Write(' ');
    }
  }

  public void WriteNewline() {
    Writer.Write('\n');
    WriteIndent();
  }

  public void Write(char c) { Writer.Write(c); }

  public void Write(string s) { Writer.Write(s); }
}
