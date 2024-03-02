using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class Forall : Function {
  public Forall(string name, NodePos pos, int outputNodeId, BlockFacing face)
      : base(name, pos, outputNodeId, face) {}

  public override void EmitConstruct(CoqEmitter emitter,
                                     bool app_needs_parens) {
    if (app_needs_parens) {
      emitter.Write('(');
    }
    emitter.Write("forall");
    foreach (Parameter p in _parameters.Parameters) {
      if (p.Type != null) {
        emitter.Write(" (");
        emitter.Write(emitter.GetName(p));
        emitter.Write(": ");
        EmitReference(p.Type, emitter, false);
        emitter.Write(")");
      } else {
        emitter.Write(' ');
        emitter.Write(emitter.GetName(p));
      }
    }
    emitter.Write(", ");
    EmitReference(_parameters.Result, emitter, false);
    if (app_needs_parens) {
      emitter.Write(')');
    }
  }
}
