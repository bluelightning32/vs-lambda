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
  public TermInput Type;
  public override IReadOnlyList<Token> Children {
    get { return _parameters.GetChildrenAtLevel(_parameters.GetNext(this)); }
  }

  public Parameter(string name, NodePos pos, ConstructRoot construct,
                   ParameterList parameters)
      : base(name) {
    Pos = pos;
    _construct = construct;
    _parameters = parameters;
  }
}