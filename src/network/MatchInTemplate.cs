using System;
using System.Diagnostics;

namespace Lambda.Network;

using Lambda.Token;

using Vintagestory.API.MathTools;

public class MatchInTemplate : BlockNodeTemplate, IAcceptScopePort {
  private readonly int _parameterId = -1;
  // The result node is the function body.
  private readonly int _resultId = -1;
  private readonly int _scopeId = -1;
  private readonly int _matchId = -1;
  private readonly BlockFacing _face;
  public MatchInTemplate(NodeAccessor accessor, Manager manager, string face,
                         NodeTemplate[] nodeTemplates)
      : base(accessor, manager, face, nodeTemplates) {
    _face = BlockFacing.FromCode(face);
    if (_face == null) {
      throw new ArgumentException($"Unknown face code {face}.");
    }
    for (int i = 0; i < nodeTemplates.Length; ++i) {
      NodeTemplate nodeTemplate = nodeTemplates[i];
      if (nodeTemplate.Network == NetworkType.Placeholder) {
        continue;
      }
      string name = nodeTemplate.Name;
      if (name == "match") {
        Debug.Assert(_matchId == -1);
        _matchId = i;
      } else if (name == "scope") {
        Debug.Assert(_scopeId == -1);
        _scopeId = i;
      } else if (name == "parameter" || name == "result") {
        if (nodeTemplate.IsSource) {
          if (_parameterId != -1) {
            throw new ArgumentException(
                "Only one parameter port is allowed on the function block. Use scope blocks to add more parameters.");
          }
          _parameterId = i;
        } else {
          if (_resultId != -1) {
            throw new ArgumentException(
                "Only one result port is allowed on the function block.");
          }
          _resultId = i;
        }
      } else {
        throw new ArgumentException($"Unknown node {name}.");
      }
    }
    if (_matchId == -1) {
      throw new ArgumentException("Missing match node.");
    }
  }

  private MatchIn GetMatchIn(TokenEmissionState state, NodePos matchPos,
                             Node[] nodes, string inventoryTerm, int forNode) {
    MatchIn c;
    if (state.Prepared.TryGetValue(matchPos, out Token caseToken)) {
      c = (MatchIn)caseToken;
    } else {
      Node matchNode = nodes[matchPos.NodeId];
      c = state.AddMatchIn(matchNode.Source, matchPos, _scopeId, _face,
                           inventoryTerm);
      state.AddPrepared(matchPos, c, matchPos);
      state.AddPending(matchPos);
      foreach (int child in new int[] { _scopeId, _parameterId, _resultId }) {
        if (child != -1) {
          NodePos childPos = new(matchPos.Block, child);
          c.AddRef(state, childPos);
          if (child != forNode) {
            state.MaybeAddPendingSource(childPos, nodes);
          }
        }
      }
    }
    return c;
  }

  public Token AddPort(TokenEmissionState state, NodePos sourcePos,
                       Node[] nodes, string inventoryTerm, BlockPos childPos,
                       NodeTemplate child) {
    return GetMatchIn(state, sourcePos, nodes, inventoryTerm, -1)
        .AddPort(state, new NodePos(childPos, child.Id), child.Name,
                 child.IsSource);
  }

  private MatchIn EmitMatchIn(TokenEmissionState state, BlockPos pos,
                              Node[] nodes, string inventoryTerm) {
    NodePos matchPos = new(pos, _matchId);
    MatchIn c;
    if (state.Prepared.TryGetValue(matchPos, out Token caseToken)) {
      c = (MatchIn)caseToken;
    } else {
      Node matchNode = nodes[matchPos.NodeId];
      c = state.AddMatchIn(matchNode.Source, matchPos, _scopeId, _face,
                           inventoryTerm);
      state.AddPrepared(matchPos, c, matchPos);
      foreach (int child in new int[] { _scopeId, _parameterId, _resultId }) {
        if (child != -1) {
          NodePos childPos = new(matchPos.Block, child);
          c.AddRef(state, childPos);
          state.MaybeAddPendingSource(childPos, nodes);
        }
      }
    }
    NodePos matchSourcePos = nodes[_matchId].Source;
    Token matchSource = state.Prepared[matchSourcePos];
    matchSource.AddPendingChildren(state, _nodeTemplates[_matchId].Network,
                                   GetDownstream(matchPos));
    matchSource.ReleaseRef(state, matchPos);
    c.ReleaseRef(state, matchPos);
    return c;
  }

  public override Token Emit(TokenEmissionState state, NodePos pos,
                             Node[] nodes, string inventoryTerm) {
    if (pos.NodeId == _matchId) {
      Token matchResult = EmitMatchIn(state, pos.Block, nodes, inventoryTerm);
      state.VerifyInvariants();
      return matchResult;
    }
    NodeTemplate nodeTemplate = _nodeTemplates[pos.NodeId];
    MatchIn c = GetMatchIn(state, new NodePos(pos.Block, _matchId), nodes,
                           inventoryTerm, pos.NodeId);
    Token result;
    if (pos.NodeId == _scopeId) {
      state.AddPrepared(pos, c);
      c.AddPendingChildren(state, nodeTemplate.Network, GetDownstream(pos));
      result = c;
    } else if (pos.NodeId == _parameterId) {
      Debug.Assert(nodeTemplate.IsSource);
      result = c.AddPort(state, pos, nodeTemplate.Name, true);
      if (result.PendingRef == 0) {
        state.AddPrepared(pos, result, pos);
      } else {
        result.AddRef(state, pos);
      }
      result.AddPendingChildren(state, nodeTemplate.Network,
                                GetDownstream(pos));
      result.ReleaseRef(state, pos);
    } else {
      Debug.Assert(pos.NodeId == _resultId);
      Debug.Assert(!nodeTemplate.IsSource);
      result = c.AddPort(state, pos, nodeTemplate.Name, false);
    }
    c.ReleaseRef(state, pos);
    state.VerifyInvariants();
    return result;
  }
}
