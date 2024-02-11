using System;
using System.Collections.Generic;

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

  public Constant(NodePos pos, string term) : base(term) {
    Pos = pos;
    Term = term;
  }
}