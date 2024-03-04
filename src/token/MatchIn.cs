using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class MatchIn : Case {
  public MatchIn(string name, NodePos matchPos, Match match, int scopeNodeId,
                 BlockFacing face)
      : base(name, matchPos, match, scopeNodeId, face) {}

  public override void EmitExpression(CoqEmitter emitter,
                                      bool app_needs_parens) {
    emitter.Write(' ', null);
    emitter.Write("in ", this);
    emitter.Write(Name, this);
    foreach (Parameter p in _parameters.Parameters) {
      if (p.HasSinks) {
        emitter.Write(emitter.GetName(p), p);
      } else {
        emitter.Write('_', p);
      }
      emitter.Write(' ', null);
    }
    emitter.Write("return", this);
    emitter.Write(' ', null);
    EmitReference(_parameters.Result, emitter, false);
  }
}
