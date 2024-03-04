using System;
using System.Collections.Generic;

using Lambda.Network;

namespace Lambda.Token;

public class Parameter : TermSource {
  public readonly NodePos Pos;
  public override NodePos FirstBlock => Pos;

  public override IReadOnlyList<NodePos> Blocks => new NodePos[] { Pos };

  public override IReadOnlyList<NodePos> ScopeMatchConnectors =>
      Array.Empty<NodePos>();

  private ConstructRoot _construct;
  public override ConstructRoot Construct => _construct;

  readonly ParameterList _parameters;
  public TermInput Type;

  public override IReadOnlyList<Token> Children {
    get { return _parameters.GetChildrenAtLevel(_parameters.GetNext(this)); }
  }

  public override string Name { get; }

  public Parameter(string name, NodePos pos, ConstructRoot construct,
                   ParameterList parameters) {
    Name = name;
    Pos = pos;
    _construct = construct;
    _parameters = parameters;
  }

  public override void Dispose() {
    _construct?.Dispose();
    _construct = null;
    _parameters.Dispose();
    Type?.Dispose();
    Type = null;
    base.Dispose();
  }

  public void AddUnused(ConstructRoot constructRoot) {
    GetOrCreateResult().AddUnused(constructRoot);
  }

  private TermInput GetOrCreateResult() {
    _parameters.Result ??= new TermInput(
        "placeholder", _parameters.Parameters.Max.Blocks[0], _construct);
    return _parameters.Result;
  }

  public override void EmitExpression(CoqEmitter emitter,
                                      bool app_needs_parens) {
    emitter.Write(emitter.GetName(this));
  }
}
