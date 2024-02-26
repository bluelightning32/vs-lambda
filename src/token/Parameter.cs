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
  // Entries in this list that have an IncomingEdge count of 1 are entries that
  // are otherwise unused constructs. They have been anchored to this parameter,
  // because they (or a dependent) use this parameter.
  //
  // Entries in this list that have an IncomingEdge count of 2 or greater are
  // constructs that are used multiple times under this branch.
  public List<ConstructRoot> _anchored = null;
  public override IReadOnlyList<Token> Children {
    get {
      Token[] children =
          _parameters.GetChildrenAtLevel(_parameters.GetNext(this));
      if (_anchored == null || _anchored.Count == 0) {
        return children;
      }
      return children.Append(_anchored);
    }
  }

  public IReadOnlyList<ConstructRoot> Anchored {
    get =>
        (IReadOnlyList<ConstructRoot>)_anchored ?? Array.Empty<ConstructRoot>();
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
    if (_anchored != null) {
      List<ConstructRoot> unused = _anchored;
      _anchored = null;
      foreach (ConstructRoot c in unused) {
        c.Dispose();
      }
    }
    base.Dispose();
  }

  public void AddUnused(ConstructRoot constructRoot) {
    _anchored ??= new List<ConstructRoot>();
    Debug.Assert(!_anchored.Contains(constructRoot));
    _anchored.Add(constructRoot);
  }

  public override void WriteOutsideEdges(GraphvizEmitter state) {
    if (_anchored == null || _anchored.Count == 0) {
      return;
    }
    state.StartSubgraph($"unused_{state.GetName(this)}", "blue");
    foreach (ConstructRoot c in _anchored) {
      state.WriteEdge(this, c);
    }
    state.EndSubgraph();
  }

  protected override void
  ScopeMultiuseVisitChildren(Dictionary<Token, int> visited,
                             List<ConstructRoot> ready) {
    base.ScopeMultiuseVisitChildren(visited, ready);
    while (ready.Count > 0) {
      List<ConstructRoot> newReady = new();
      foreach (ConstructRoot c in ready) {
        _anchored ??= new();
        _anchored.Add(c);
        c.ScopeMultiuseReady(visited, newReady);
      }
      ready.Clear();
      ready = newReady;
    }
  }
}