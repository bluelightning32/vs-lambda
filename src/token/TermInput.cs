using System;
using System.Collections.Generic;
using System.Linq;

using Lambda.Network;

namespace Lambda.Token;

public class TermInput : Token {
  NodePos _pos;
  public override IReadOnlyList<NodePos> Blocks => new NodePos[] { _pos };

  public override IReadOnlyList<NodePos> ScopeMatchConnectors =>
      Array.Empty<NodePos>();
  public override IReadOnlyList<NodePos> TermConnectors =>
      Array.Empty<NodePos>();

  private ConstructRoot _construct;
  public override ConstructRoot Construct => _construct;

  public Token Value { get; protected set; }
  public override IReadOnlyList<Token> Children =>
      Value == null ? Array.Empty<Token>() : new Token[] { Value };

  public TermInput(string name, NodePos pos, ConstructRoot construct)
      : base(name) {
    _pos = pos;
    _construct = construct;
  }

  public virtual void SetSource(Token source) { Value = source; }

  public override void Dispose() {
    _construct?.Dispose();
    _construct = null;
    base.Dispose();
  }

  public override void WriteOutsideEdges(GraphvizEmitter state) {
    if (Value != null) {
      state.WriteEdge(this, Value);
    }
  }
}