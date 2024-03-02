using System;
using System.Collections.Generic;
using System.Diagnostics;
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
  public override IReadOnlyList<Token> Children {
    get {
      if (_anchored == null && Value == null) {
        return Array.Empty<Token>();
      }
      List<Token> result = new();
      if (Value != null) {
        result.Add(Value);
      }
      if (_anchored != null) {
        result.AddRange(_anchored);
      }
      return result;
    }
  }

  // Entries in this list that have an IncomingEdge count of 1 are entries that
  // are otherwise unused constructs. They have been anchored to this branch,
  // because they (or a dependent) use parameter in this branch.
  //
  // Entries in this list that have an IncomingEdge count of 2 or greater are
  // constructs that are used multiple times under this branch.
  //
  // The
  private List<ConstructRoot> _anchored = null;

  public IReadOnlyList<ConstructRoot> Anchored {
    get =>
        (IReadOnlyList<ConstructRoot>)_anchored ?? Array.Empty<ConstructRoot>();
  }

  public TermInput(string name, NodePos pos, ConstructRoot construct)
      : base(name) {
    _pos = pos;
    _construct = construct;
  }

  public virtual void SetSource(Token source) { Value = source; }

  public override void Dispose() {
    _construct?.Dispose();
    _construct = null;
    if (_anchored != null) {
      List<ConstructRoot> anchored = _anchored;
      _anchored = null;
      foreach (ConstructRoot c in anchored) {
        c.Dispose();
      }
    }
    base.Dispose();
  }

  public override void WriteOutsideEdges(GraphvizEmitter state) {
    if (Value != null) {
      state.WriteEdge(this, Value);
    }
    if (_anchored == null || _anchored.Count == 0) {
      return;
    }
    state.StartSubgraph($"anchored_{state.GetName(this)}", "blue");
    foreach (ConstructRoot c in _anchored) {
      state.WriteEdge(this, c);
    }
    state.EndSubgraph();
  }

  protected override void ScopeMultiuseVisitChildren(AnchorPoint tracker) {
    AnchorPoint myTracker = tracker.CreateSubtracker();
    foreach (Token c in Children) {
      // Ignore TermInput to Parameter edges to prevent cycles.
      if (c is not Parameter) {
        c.ScopeMultiuse(myTracker, true);
      }
    }
    for (int i = 0; i < myTracker.ReadyCount; ++i) {
      ConstructRoot c = myTracker.Ready[i];
      c.ScopeMultiuseReady(myTracker);
    }
    if (myTracker.ReadyCount > 0) {
      _anchored ??= new();
      _anchored.AddRange(myTracker.Ready);
      _anchored.Reverse(_anchored.Count - myTracker.Ready.Count,
                        myTracker.Ready.Count);
    }
    tracker.ReleaseSubtracker(myTracker);
  }

  public void AddUnused(ConstructRoot constructRoot) {
    _anchored ??= new List<ConstructRoot>();
    Debug.Assert(!_anchored.Contains(constructRoot));
    _anchored.Add(constructRoot);
  }
}