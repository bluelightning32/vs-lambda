using System;
using System.Diagnostics;

namespace Lambda.Token;

public abstract class ConstructRoot : TermSource {
  // Number of edges that point to this element.
  public int IncomingEdgeCount { get; private set; }

  public ConstructRoot(string name) : base(name) {}

  // This is only non-null during multiuse scoping for constructs that need to
  // be anchored. After the construct is anchored, this is set back to null.
  private AnchorPoint _anchorPoint = null;

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

  public override void ScopeMultiuse(AnchorPoint tracker, bool isUse) {
    // If isUse is false, then this construct is visited as a top-level
    // unreferenced root.
    if (isUse) {
      ++_multiUseVisited;
      Debug.Assert(_multiUseVisited <= IncomingEdgeCount);
      if (IncomingEdgeCount > 1) {
        if (_multiUseVisited == IncomingEdgeCount) {
          _anchorPoint.AddReady(this);
          // Dereference the anchor point so that it can get garbage collected.
          _anchorPoint = null;
        } else if (_multiUseVisited == 1) {
          _anchorPoint = tracker;
          tracker.AddReference();
        }
        return;
      }
    }
    ScopeMultiuseVisitChildren(tracker);
  }

  public void ScopeMultiuseReady(AnchorPoint tracker) {
    if (IncomingEdgeCount == 1) {
      throw new InvalidOperationException(
          "ScopeMultiuseReady should only be called for nodes with 2 or more references.");
    }
    ScopeMultiuseVisitChildren(tracker);
  }

  public virtual void EmitDefinition(string name, CoqEmitter emitter) {
    emitter.Write("Definition ");
    emitter.Write(name);
    EmitDefinitionType(emitter);
    emitter.Write(":=");
    emitter.WriteNewline();
    EmitConstruct(emitter, false);
    emitter.Write(".");
    emitter.WriteNewline();
  }

  protected virtual void EmitDefinitionType(CoqEmitter emitter) {}

  public void EmitLet(CoqEmitter emitter) {
    emitter.Write("let ");
    emitter.Write(emitter.GetName(this));
    emitter.Write(" :=");
    emitter.AddIndent();
    emitter.WriteNewline();
    EmitConstruct(emitter, false);
    emitter.ReleaseIndent();
    emitter.Write(" in");
    emitter.WriteNewline();
  }

  public override void EmitExpression(CoqEmitter emitter,
                                      bool app_needs_parens) {
    if (IncomingEdgeCount <= 1) {
      EmitConstruct(emitter, app_needs_parens);
    } else {
      emitter.Write(emitter.GetName(this));
    }
  }

  public abstract void EmitConstruct(CoqEmitter emitter, bool app_needs_parens);

  // This is called after all children pointing to this construct are processed.
  public virtual void Finished() {}
}
