using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class Forall : Function {
  public override string Name => "forall";

  public Forall(NodePos pos, int outputNodeId, BlockFacing face)
      : base(pos, outputNodeId, face) {}

  public override void EmitConstruct(CoqEmitter emitter,
                                     bool app_needs_parens) {
    if (app_needs_parens) {
      emitter.Write('(', this);
    }
    emitter.Write("forall", this);
    foreach (Parameter p in _parameters.Parameters) {
      if (p.Type != null) {
        emitter.Write(' ', null);
        emitter.Write('(', p);
        emitter.Write(emitter.GetName(p), p);
        emitter.Write(':', p);
        emitter.Write(' ', p);
        EmitReference(p.Type, emitter, false);
        emitter.Write(')', p);
      } else {
        emitter.Write(' ', null);
        emitter.Write(emitter.GetName(p), p);
      }
    }
    emitter.Write(',', this);
    emitter.Write(' ', null);
    EmitReference(_parameters.Result, emitter, false);
    if (app_needs_parens) {
      emitter.Write(')', this);
    }
  }
}
