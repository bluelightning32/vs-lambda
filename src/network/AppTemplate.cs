using System;

namespace Lambda.Network;

using System.Collections.Generic;

using Lambda.Token;

using Vintagestory.API.MathTools;

public class AppTemplate : BlockNodeTemplate {
  private readonly int _outputId = -1;
  private readonly int _applicand = -1;
  private readonly int _argument = -1;
  public AppTemplate(NodeAccessor accessor, Manager manager, string face,
                     NodeTemplate[] nodeTemplates)
      : base(accessor, manager, face, nodeTemplates) {
    for (int i = 0; i < nodeTemplates.Length; ++i) {
      NodeTemplate nodeTemplate = nodeTemplates[i];
      if (nodeTemplate.Network == NetworkType.Placeholder) {
        continue;
      }
      string name = nodeTemplate.Name;
      if (name == "output") {
        if (_outputId != -1) {
          throw new ArgumentException("Only one output port is allowed.");
        }
        if (nodeTemplate.IsSource) {
          _outputId = i;
        } else {
          throw new ArgumentException("Output port direction must be Out.");
        }
      } else if (name == "applicand") {
        if (_applicand != -1) {
          throw new ArgumentException("Only one applicand port is allowed.");
        }
        if (!nodeTemplate.IsSource) {
          _applicand = i;
        } else {
          throw new ArgumentException("Applicand port direction must be In.");
        }
      } else if (name == "argument") {
        if (_argument != -1) {
          throw new ArgumentException("Only one argument port is allowed.");
        }
        if (!nodeTemplate.IsSource) {
          _argument = i;
        } else {
          throw new ArgumentException("Argument port direction must be In.");
        }
      } else {
        throw new ArgumentException($"Unknown node {name}.");
      }
    }
  }

  // Return the position of the preferred node, or if it is -1, then the
  // position of some other node.
  private NodePos GetNodePosOrDefault(BlockPos pos, int preferredNode) {
    if (preferredNode != -1) {
      return new NodePos(pos, preferredNode);
    }
    if (_outputId != -1) {
      return new NodePos(pos, _outputId);
    }
    if (_applicand != -1) {
      return new NodePos(pos, _applicand);
    }
    if (_argument != -1) {
      return new NodePos(pos, _argument);
    }
    return new NodePos(pos, 0);
  }

  private void CreateAll(TokenEmitter state, BlockPos pos, Node[] nodes,
                         string inventoryTerm, int forNode) {
    List<Token> added = new();
    NodePos outputPos = GetNodePosOrDefault(pos, _outputId);
    if (_applicand == -1 && _argument == -1) {
      added.Add(new Constant(outputPos, inventoryTerm));
    } else {
      App app = new("app", outputPos, GetNodePosOrDefault(pos, _applicand),
                    GetNodePosOrDefault(pos, _argument));
      added.Add(app);
      if (_applicand != -1) {
        added.Add(app.Applicand);
      } else {
        Constant appConstant = new(outputPos, inventoryTerm);
        appConstant.AddSink(state, app.Applicand);
      }
      if (_argument != -1) {
        added.Add(app.Argument);
      } else {
        Constant argConstant = new(outputPos, inventoryTerm);
        argConstant.AddSink(state, app.Argument);
      }
    }
    foreach (Token a in added) {
      int nodeId = a.Blocks[0].NodeId;
      if (nodeId != forNode) {
        NodePos source = nodes[a.Blocks[0].NodeId].Source;
        if (!nodes[nodeId].IsConnected()) {
          source = a.Blocks[0];
        }
        // If `MaybeAddPendingSource` is going to be called, it must be called
        // before `AddPrepared`, because `MaybeAddPendingSource` assumes the
        // source is already pending if it is prepared.
        state.MaybeAddPendingSource(source);
      }
      state.AddPrepared(a.Blocks[0], a, a.Blocks[0]);
    }
  }

  public override Token Emit(TokenEmitter state, NodePos pos, Node[] nodes,
                             string inventoryTerm) {
    if (!state.Prepared.TryGetValue(pos, out Token result)) {
      CreateAll(state, pos.Block, nodes, inventoryTerm, pos.NodeId);
      result = state.Prepared[pos];
    }
    NodeTemplate nodeTemplate = _nodeTemplates[pos.NodeId];
    if (pos.NodeId == _outputId) {
      result.AddPendingChildren(state, nodeTemplate.Network,
                                GetDownstream(pos));
    } else {
      if (nodes[pos.NodeId].IsConnected()) {
        NodePos sourcePos = nodes[pos.NodeId].Source;
        Token source = state.Prepared[sourcePos];
        source.AddSink(state, result);
        source.ReleaseRef(state, pos);
      }
    }
    result.ReleaseRef(state, pos);
    state.VerifyInvariants();
    return result;
  }
}
