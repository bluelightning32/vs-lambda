using System;
using System.Collections.Generic;
using System.Text;

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

  public string _name;
  public override string Name => _name ?? "parameter";

  public void SetName(string name) { _name = name; }

  public Parameter(NodePos pos, ConstructRoot construct,
                   ParameterList parameters) {
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
    emitter.Write(emitter.GetName(this), this);
  }

  public override void GetPreferredIdentifier(StringBuilder sb) {
    if (_name != null) {
      SanitizeIdentifier(_name, sb);
      return;
    }
    base.GetPreferredIdentifier(sb);
  }

  public override void GatherImports(CoqEmitter emitter) {}
}
