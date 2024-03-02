using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class MatchIn : Case {
  public MatchIn(string name, NodePos matchPos, Match match, int scopeNodeId,
                 BlockFacing face)
      : base(name, matchPos, match, scopeNodeId, face) {}

  public override void EmitExpression(CoqEmitter emitter,
                                      bool app_needs_parens) {
    emitter.Write(" in ");
    emitter.Write(Name);
    foreach (Parameter p in _parameters.Parameters) {
      emitter.GetName(p);
      emitter.Write(' ');
    }
    emitter.Write(" return ");
    EmitReference(_parameters.Result, emitter, false);
  }
}
