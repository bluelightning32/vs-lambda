using System.Collections.Generic;
using System.IO;
using System.Text;

using Lambda.Network;

namespace Lambda.Token;

public class GraphvizState {
  private readonly Dictionary<Token, string> _tokenNames = new();
  private readonly HashSet<string> _usedNames = new();
  private readonly List<ConstructRoot> _pending = new();

  private readonly TextWriter _writer;

  public GraphvizState(TextWriter writer) { _writer = writer; }

  public void Add(Token t) {
    GetName(t);
    DrainPending();
  }

  public string GetName(Token t) {
    if (_tokenNames.TryGetValue(t, out string name)) {
      return name;
    }
    name = AddName(t);
    _tokenNames.Add(t, name);
    _usedNames.Add(name);
    if (t.Construct == t) {
      _pending.Add(t.Construct);
    } else {
      GetName(t.Construct);
    }
    return name;
  }

  private string AddName(Token t) {
    StringBuilder sb = new();
    sb.Append(t.Name);
    sb.Append("_");
    // The foreach only looks at the first position, if there are 1 or more
    // positions.
    foreach (NodePos firstPos in t.Blocks) {
      sb.Append(firstPos);
      break;
    }
    string attempt = sb.ToString();
    if (_usedNames.Add(attempt)) {
      return attempt;
    }
    sb.Append("_");
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

  private void DrainPending() {
    for (int i = 0; i < _pending.Count; ++i) {
      _pending[i].WriteConstruct(this);
    }
    _pending.Clear();
  }

  public void WriteHeader(string name) {
    _writer.WriteLine($"digraph {name} {{");
    _writer.WriteLine("  splines=true;");
    _writer.WriteLine("  node [shape=box];");
  }

  public void WriteFooter() { _writer.WriteLine("}"); }

  public void WriteClusterHeader(string name, string label) {
    StartSubgraph($"cluster_{name}", "#00000050");
    _writer.WriteLine($"    label=\"{label}\";");
  }

  public void StartSubgraph(string name, string edgeColor) {
    _writer.WriteLine("");
    _writer.WriteLine($"  subgraph \"{name}\" {{");
    _writer.WriteLine($"    edge [color=\"{edgeColor}\"];");
  }

  public void EndSubgraph() { _writer.WriteLine("  }"); }
  public void WriteClusterFooter() { EndSubgraph(); }

  public void WriteSubgraphNode(string name, string label) {
    _writer.WriteLine($"    \"{name}\"[label=\"{label}\"];");
  }

  public void WriteSubgraphNode(Token token) {
    WriteSubgraphNode(GetName(token), $"{token.Depth}: {token.Name}");
  }

  public void WriteSubgraphEdge(string source, string target) {
    _writer.WriteLine($"    \"{source}\" -> \"{target}\";");
  }

  public void WriteSubgraphEdge(Token source, Token target) {
    WriteSubgraphEdge(GetName(source), GetName(target));
  }

  public void WriteEdge(string source, string target, bool reverse) {
    if (reverse) {
      _writer.WriteLine($"  \"{source}\" -> \"{target}\" [dir=back];");
    } else {
      _writer.WriteLine($"  \"{source}\" -> \"{target}\";");
    }
  }

  public void WriteEdge(Token source, Token target) {
    if (target is Parameter) {
      WriteEdge(GetName(target), GetName(source), true);
    } else {
      WriteEdge(GetName(source), GetName(target), false);
    }
  }
}
