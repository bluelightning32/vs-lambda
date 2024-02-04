using System;
using System.Collections.Generic;

using Lambda.Network;

namespace Lambda.Token;

public class Input : Token {
  NodePos _pos;
  public override IReadOnlyList<NodePos> Blocks => new NodePos[] { _pos };

  public override IReadOnlyList<NodePos> ScopeMatchConnectors =>
      Array.Empty<NodePos>();
  public override IReadOnlyList<NodePos> TermConnectors =>
      Array.Empty<NodePos>();

  private readonly ConstructRoot _construct;
  public override ConstructRoot Construct => _construct;

  public Token Value { get; protected set; }
  public override IReadOnlyList<Token> Children =>
      Value == null ? Array.Empty<Token>() : new Token[] { Value };

  public Input(NodePos pos, ConstructRoot construct) {
    _pos = pos;
    _construct = construct;
  }
}