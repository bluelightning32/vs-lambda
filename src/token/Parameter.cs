using System;
using System.Collections.Generic;

using Lambda.Network;

namespace Lambda.Token;

public class Parameter : TermSource {
  public readonly NodePos Pos;
  public override IReadOnlyList<NodePos> Blocks => new NodePos[] { Pos };

  public override IReadOnlyList<NodePos> ScopeMatchConnectors =>
      Array.Empty<NodePos>();

  private readonly ConstructRoot _construct;
  public override ConstructRoot Construct => _construct;

  readonly ParameterList _parameters;
#pragma warning disable 0649
  public Input Type;
#pragma warning restore 0649
  public override IReadOnlyList<Token> Children {
    get { return _parameters.GetChildrenAtLevel(_parameters.GetNext(this)); }
  }

  public Parameter(NodePos pos, ConstructRoot construct,
                   ParameterList parameters) {
    Pos = pos;
    _construct = construct;
    _parameters = parameters;
  }
}