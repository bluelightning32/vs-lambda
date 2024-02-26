using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lambda.Token;

public abstract class ConstructRoot : TermSource {
  // Number of edges that point to this element.
  public int IncomingEdgeCount { get; private set; }

  public ConstructRoot(string name) : base(name) {}

  public abstract void WriteConstruct(GraphvizEmitter state);

  public override void AddSink(TokenEmitter state, Token sink) {
    if (sink is TermInput input) {
      input.SetSource(this);
      ++IncomingEdgeCount;
    } else {
      base.AddSink(state, sink);
    }
  }

  public void AddAnchor(Parameter p) {
    Debug.Assert(IncomingEdgeCount == 0);
    p.AddUnused(this);
    ++IncomingEdgeCount;
  }

  public override void ScopeMultiuse(Dictionary<Token, int> visited,
                                     List<ConstructRoot> ready, bool isUse) {
    // If isUse is false, then this construct is visited as a top-level
    // unreferenced root.
    if (isUse) {
      visited.TryAdd(this, 0);
      int newCount = ++visited[this];
      Debug.Assert(newCount <= IncomingEdgeCount);
      if (IncomingEdgeCount > 1) {
        if (newCount == IncomingEdgeCount) {
          ready.Add(this);
        }
        return;
      }
    }
    ScopeMultiuseVisitChildren(visited, ready);
  }

  public void ScopeMultiuseReady(Dictionary<Token, int> visited,
                                 List<ConstructRoot> ready) {
    if (IncomingEdgeCount == 1) {
      throw new InvalidOperationException(
          "ScopeMultiuseReady should only be called for nodes with 2 or more references.");
    }
    ScopeMultiuseVisitChildren(visited, ready);
  }
}
