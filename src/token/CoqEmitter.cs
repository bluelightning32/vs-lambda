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

  // Converts an arbitrary string into an identifier, by replacing invalid
  // characters with underscores.
  private static void SanitizeIdentifier(string name, StringBuilder sb) {
    if (name.Length == 0) {
      sb.Append("empty");
      return;
    }
    if (Char.IsLetter(name[0])) {
      sb.Append(name[0]);
    } else if (Char.IsAsciiDigit(name[0])) {
      sb.Append('_');
      sb.Append(name[0]);
    } else {
      sb.Append('_');
      if (name.Length == 1) {
        // A single underscore is a reserved identifier. So double it up.
        sb.Append('_');
        return;
      }
    }

    for (int i = 1; i < name.Length; ++i) {
      char c = name[i];
      if (Char.IsLetterOrDigit(c) || c == '\'') {
        sb.Append(c);
      } else {
        sb.Append('_');
      }
    }
  }

  private string AddName(Token t) {
    StringBuilder sb = new();
    SanitizeIdentifier(t.Name, sb);
    sb.Append('_');
    // The foreach only looks at the first position, if there are 1 or more
    // positions.
    foreach (NodePos firstPos in t.Blocks) {
      sb.Append(firstPos.Block.X);
      sb.Append('_');
      sb.Append(firstPos.Block.Y);
      sb.Append('_');
      sb.Append(firstPos.Block.Z);
      sb.Append('_');
      sb.Append(firstPos.Block.dimension);
      sb.Append('_');
      sb.Append(firstPos.NodeId);
      break;
    }
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
