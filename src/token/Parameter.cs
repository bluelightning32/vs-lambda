using System;
using System.Collections.Generic;
using System.Diagnostics;

using Lambda.Network;

using Vintagestory.API.Util;

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
  // Unused constructs that have been anchored to this parameter because they or
  // a dependent use this parameter.
  public List<ConstructRoot> _unused = null;
  public override IReadOnlyList<Token> Children {
    get {
      Token[] children =
          _parameters.GetChildrenAtLevel(_parameters.GetNext(this));
      if (_unused == null || _unused.Count == 0) {
        return children;
      }
      return children.Append(_unused);
    }
  }

  public IReadOnlyList<ConstructRoot> Unused {
    get =>
        (IReadOnlyList<ConstructRoot>)_unused ?? Array.Empty<ConstructRoot>();
  }

  public Parameter(string name, NodePos pos, ConstructRoot construct,
                   ParameterList parameters)
      : base(name) {
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
    if (_unused != null) {
      List<ConstructRoot> unused = _unused;
      _unused = null;
      foreach (ConstructRoot c in unused) {
        c.Dispose();
      }
    }
    base.Dispose();
  }

  internal override void SetDepth(Token parent) {
    if (parent is TermInput) {
      return;
    }
    base.SetDepth(parent);
  }

  public void AddUnused(ConstructRoot constructRoot) {
    _unused ??= new List<ConstructRoot>();
    Debug.Assert(!_unused.Contains(constructRoot));
    _unused.Add(constructRoot);
  }

  public override void WriteOutsideEdges(GraphvizState state) {
    if (_unused == null || _unused.Count == 0) {
      return;
    }
    state.StartSubgraph($"unused_{state.GetName(this)}", "blue");
    foreach (ConstructRoot c in _unused) {
      state.WriteEdge(this, c);
    }
    state.EndSubgraph();
  }
}