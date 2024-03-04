using System;
using System.Collections.Generic;
using System.Text;

using Lambda.Network;

namespace Lambda.Token;

public class Constant : ConstructRoot {
  public readonly NodePos Pos;
  public readonly string Term;
  public override IReadOnlyList<NodePos> Blocks => new NodePos[] { Pos };

  public override IReadOnlyList<NodePos> ScopeMatchConnectors =>
      Array.Empty<NodePos>();

  public override ConstructRoot Construct => this;

  public override IReadOnlyList<Token> Children { get => Array.Empty<Token>(); }

  public override string Name { get; }

  static readonly private char[] NameStopAt = new char[] { ' ' };

  private static string CreateName(string term) {
    int stop = term.IndexOfAny(NameStopAt);
    if (stop == -1) {
      return term;
    }
    if (stop == 0) {
      return "const";
    }
    return term.Substring(0, stop);
  }

  public Constant(NodePos pos, string term) {
    Name = CreateName(term);
    Pos = pos;
    Term = term;
  }

  public override void WriteConstruct(GraphvizEmitter state) {
    string name = state.GetName(this);
    StringBuilder label = new();
    label.Append("Constant");
    bool first = true;
    foreach (NodePos pos in Blocks) {
      if (first) {
        label.Append($"\\n{pos}");
        first = false;
      } else {
        label.Append($",\\n{pos}");
      }
    }
    state.WriteClusterHeader(name, label.ToString());

    state.WriteSubgraphNode(this);

    state.WriteClusterFooter();
  }

  public override void EmitConstruct(CoqEmitter emitter,
                                     bool app_needs_parens) {
    bool singleTerm = true;
    foreach (char c in Term) {
      if (!Char.IsLetterOrDigit(c)) {
        singleTerm = false;
        break;
      }
    }
    if (!singleTerm) {
      emitter.Write('(');
    }
    emitter.Write(Term);
    if (!singleTerm) {
      emitter.Write(')');
    }
  }
}
