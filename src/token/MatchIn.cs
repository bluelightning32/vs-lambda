using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class MatchIn : Case {
  public MatchIn(string name, NodePos matchPos, Match match, int scopeNodeId,
                 BlockFacing face)
      : base(name, matchPos, match, scopeNodeId, face) {}
}
