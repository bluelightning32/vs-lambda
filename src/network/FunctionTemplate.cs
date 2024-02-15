using System;
using System.Diagnostics;

namespace Lambda.Network;

using Lambda.Token;

using Vintagestory.API.MathTools;

public class FunctionTemplate : BlockNodeTemplate {
  // The output node outputs the entire function. Set to -1 if there is no
  // output port.
  private readonly int _outputId = -1;
  private readonly int _parameterId = -1;
  // The result node is the function body.
  private readonly int _resultId = -1;
  private readonly int _scopeId = -1;
  private readonly BlockFacing _face;
  public FunctionTemplate(NodeAccessor accessor, Manager manager, string face,
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
      if (name == "scope") {
        Debug.Assert(_scopeId == -1);
        _scopeId = i;
      } else if (name == "output") {
        if (nodeTemplate.IsSource) {
          _outputId = i;
        } else {
          throw new ArgumentException("Output port direction must be Out.");
        }
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
        throw new ArgumentException($"Unknown scope {name}.");
      }
    }
    if (_scopeId == -1) {
      throw new ArgumentException("Missing scope node.");
    }
  }

  private Function EmitScope(TokenEmissionState state, BlockPos pos,
                             Node[] nodes, string inventoryTerm) {
    NodePos scopePos = new(pos, _scopeId);
    Function scope = new("function", scopePos, _outputId, _face);
    state.AddPrepared(scopePos, scope, scopePos);
    scope.AddPendingChildren(state, _nodeTemplates[_scopeId].Network,
                             GetDownstream(scopePos));
    foreach (int child in new int[] { _outputId, _parameterId, _resultId }) {
      if (child != -1) {
        scope.AddRef(state, new NodePos(pos, child));
      }
    }
    if (inventoryTerm != null) {
      Token resultType = scope.AddPort(state, scopePos, "resulttype", false);
      Token constant = new Constant(scopePos, inventoryTerm);
      constant.AddSink(state, resultType);
    }

    scope.ReleaseRef(state, scopePos);
    return scope;
  }

  public override Token Emit(TokenEmissionState state, NodePos pos,
                             Node[] nodes, string inventoryTerm) {
    if (pos.NodeId == _scopeId) {
      return EmitScope(state, pos.Block, nodes, inventoryTerm);
    }
    NodeTemplate nodeTemplate = _nodeTemplates[pos.NodeId];
    Function scope =
        (Function)state.GetOrCreateSource(new NodePos(pos.Block, _scopeId));
    Token result;
    if (pos.NodeId == _outputId) {
      state.AddPrepared(pos, scope);
      scope.AddPendingChildren(state, nodeTemplate.Network, GetDownstream(pos));
      result = scope;
    } else if (pos.NodeId == _parameterId) {
      Debug.Assert(nodeTemplate.IsSource);
      result = scope.AddPort(state, pos, nodeTemplate.Name, true);
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
      result = scope.AddPort(state, pos, nodeTemplate.Name, false);
    }
    scope.ReleaseRef(state, pos);
    return result;
  }
}
