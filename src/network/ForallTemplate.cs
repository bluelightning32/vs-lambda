using System;
using System.Diagnostics;

namespace Lambda.Network;

using Lambda.Token;

using Vintagestory.API.MathTools;

public class ForallTemplate : FunctionTemplate {
  public ForallTemplate(NodeAccessor accessor, Manager manager, string face,
                        NodeTemplate[] nodeTemplates)
      : base(accessor, manager, face, nodeTemplates) {}

  protected override Function CreateFunction(NodePos sourcePos,
                                             string inventoryTerm) {
    return new Forall(sourcePos, _outputId, _face);
  }
}
