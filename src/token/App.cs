using System;
using System.Collections.Generic;
using System.Text;

using Lambda.Network;

namespace Lambda.Token;

public class App : ConstructRoot {
  public readonly NodePos Pos;
  public override IReadOnlyList<NodePos> Blocks {
    get { return new NodePos[] { Pos }; }
  }

  public override ConstructRoot Construct => this;

  public override IReadOnlyList<Token> Children =>
      new List<Token>() { Applicand, Argument };

  public TermInput Applicand { get; private set; }
  public TermInput Argument { get; private set; }

  public override string Name => "app";

  public override IReadOnlyList<NodePos> ScopeMatchConnectors =>
      Array.Empty<NodePos>();

  public App(string name, NodePos pos, NodePos applicandPos,
             NodePos argumentPos) {
    Pos = pos;
    Applicand = new TermInput("applicand", applicandPos, this);
    Argument = new TermInput("argument", argumentPos, this);
  }

  public override void Dispose() {
    TermInput applicand = Applicand;
    TermInput argument = Argument;
    Applicand = null;
    Argument = null;
    applicand?.Dispose();
    argument?.Dispose();
    base.Dispose();
  }

  public override void WriteConstruct(GraphvizEmitter state) {
    string name = state.GetName(this);
    StringBuilder label = new();
    label.Append(Name);
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

    state.WriteSubgraphNode(Applicand);
    state.WriteSubgraphEdge(this, Applicand);
    state.WriteSubgraphNode(Argument);
    state.WriteSubgraphEdge(this, Argument);

    state.WriteClusterFooter();

    WriteOutsideEdges(state);
    Applicand.WriteOutsideEdges(state);
    Argument.WriteOutsideEdges(state);
  }

  public override void EmitConstruct(CoqEmitter emitter,
                                     bool app_needs_parens) {
    if (app_needs_parens) {
      emitter.Write('(');
    }
    EmitReference(Applicand, emitter, false);
    emitter.Write(' ');
    EmitReference(Argument, emitter, true);
    if (app_needs_parens) {
      emitter.Write(')');
    }
  }
}
