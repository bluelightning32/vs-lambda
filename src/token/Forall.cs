using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lambda.Network;

using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Token;

public class Forall : Function {
  public Forall(string name, NodePos pos, int outputNodeId, BlockFacing face)
      : base(name, pos, outputNodeId, face) {}
}
