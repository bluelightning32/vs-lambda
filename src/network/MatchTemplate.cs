using System;
using System.Diagnostics;

namespace Lambda.Network;

using Lambda.Token;

using Vintagestory.API.MathTools;

public class MatchTemplate : BlockNodeTemplate {
  // The output node outputs the entire match.
  private readonly int _outputId = -1;
  private readonly int _inputId = -1;
  private readonly int _matchId = -1;
  public MatchTemplate(NodeAccessor accessor, Manager manager, string face,
                       NodeTemplate[] nodeTemplates)
      : base(accessor, manager, face, nodeTemplates) {
    for (int i = 0; i < nodeTemplates.Length; ++i) {
      NodeTemplate nodeTemplate = nodeTemplates[i];
      if (nodeTemplate.Network == NetworkType.Placeholder) {
        continue;
      }
      string name = nodeTemplate.Name;
      if (name == "match") {
        Debug.Assert(_matchId == -1);
        _matchId = i;
      } else if (name == "output") {
        if (nodeTemplate.IsSource) {
          _outputId = i;
        } else {
          throw new ArgumentException("Output port direction must be Out.");
        }
      } else if (name == "input") {
        if (!nodeTemplate.IsSource) {
          _inputId = i;
        } else {
          throw new ArgumentException("Input port direction must be In.");
        }
      } else {
        throw new ArgumentException($"Unknown node {name}.");
      }
    }
    if (_matchId == -1) {
      throw new ArgumentException("Missing match node.");
    }
  }

  private Match GetMatch(TokenEmitter state, NodePos sourcePos,
                         Node[] nodes, int forNode) {
    Match source = (Match)state.TryGetSource(sourcePos);
    if (source == null) {
      source = new("match", sourcePos, _inputId, _outputId);
      state.AddPrepared(sourcePos, source, sourcePos);
      state.AddPending(sourcePos);
      foreach (int child in new int[] { _outputId, _inputId }) {
        if (child != -1) {
          NodePos childPos = new(sourcePos.Block, child);
          source.AddRef(state, childPos);
          if (child != forNode) {
            state.MaybeAddPendingSource(childPos, nodes);
          }
        }
      }
    }
    return source;
  }

  public Case AddCase(TokenEmitter state, NodePos sourcePos, Node[] nodes,
                      NodePos childMatchPos, int childScopeId, BlockFacing face,
                      string inventoryTerm) {
    Match m = GetMatch(state, sourcePos, nodes, -1);
    Case c = new(inventoryTerm, childMatchPos, m, childScopeId, face);
    m.AddCase(c);
    return c;
  }

  public MatchIn AddMatchIn(TokenEmitter state, NodePos sourcePos,
                            Node[] nodes, NodePos childMatchPos,
                            int childScopeId, BlockFacing face,
                            string inventoryTerm) {
    Match m = GetMatch(state, sourcePos, nodes, -1);
    MatchIn c = new(inventoryTerm, childMatchPos, m, childScopeId, face);
    m.AddMatchIn(c);
    return c;
  }

  private Match EmitMatch(TokenEmitter state, BlockPos pos,
                          Node[] nodes) {
    NodePos matchPos = new(pos, _matchId);
    Match match = (Match)state.TryGetSource(matchPos);
    if (match == null) {
      match = new("match", matchPos, _inputId, _outputId);
      state.AddPrepared(matchPos, match, matchPos);
      foreach (int child in new int[] { _outputId, _inputId }) {
        if (child != -1) {
          match.AddRef(state, new NodePos(pos, child));
        }
      }
    }
    match.AddPendingChildren(state, _nodeTemplates[_matchId].Network,
                             GetDownstream(matchPos));
    match.ReleaseRef(state, matchPos);
    return match;
  }

  public override Token Emit(TokenEmitter state, NodePos pos,
                             Node[] nodes, string inventoryTerm) {
    if (pos.NodeId == _matchId) {
      Token matchResult = EmitMatch(state, pos.Block, nodes);
      state.VerifyInvariants();
      return matchResult;
    }
    NodeTemplate nodeTemplate = _nodeTemplates[pos.NodeId];
    Match match =
        GetMatch(state, new NodePos(pos.Block, _matchId), nodes, pos.NodeId);
    Token result;
    if (pos.NodeId == _outputId) {
      state.AddPrepared(pos, match);
      match.AddPendingChildren(state, nodeTemplate.Network, GetDownstream(pos));
      result = match;
    } else {
      Debug.Assert(pos.NodeId == _inputId);
      Debug.Assert(!nodeTemplate.IsSource);
      result = match.Input;
      if (nodes[pos.NodeId].IsConnected()) {
        NodePos sourcePos = nodes[pos.NodeId].Source;
        Token source = state.Prepared[sourcePos];
        source.AddSink(state, result);
        source.ReleaseRef(state, pos);
      }
    }
    match.ReleaseRef(state, pos);
    state.VerifyInvariants();
    return result;
  }
}
