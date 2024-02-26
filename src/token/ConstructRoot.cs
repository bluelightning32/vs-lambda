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
}
