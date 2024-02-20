namespace Lambda.Network;

using Lambda.Token;

public class MatchInTemplate : CaseTemplate {
  public MatchInTemplate(NodeAccessor accessor, Manager manager, string face,
                         NodeTemplate[] nodeTemplates)
      : base(accessor, manager, face, nodeTemplates) {}

  protected override Case CreateCase(TokenEmissionState state, NodePos matchPos,
                                     Node[] nodes, string inventoryTerm) {
    Node matchNode = nodes[matchPos.NodeId];
    return state.AddMatchIn(matchNode.Source, matchPos, _scopeId, _face,
                            inventoryTerm);
  }
}
