using System;
using System.Diagnostics;

namespace Lambda.Network;

using System.Reflection;
using System.Text.RegularExpressions;

using Lambda.Token;

using Vintagestory.API.MathTools;

public class ScopeTemplate : BlockNodeTemplate {
  public ScopeTemplate(NodeAccessor accessor, Manager manager, string face,
                       NodeTemplate[] nodeTemplates)
      : base(accessor, manager, face, nodeTemplates) {}

  // Creates all of the port tokens. The port tokens are addref'd for the parent
  // node and all other ports, minus the parent node if it matches `forPos`. If
  // `forPos` matches one of the port tokens, then that token is returned.
  // Otherwise, null is returned.
  //
  // All ports and the parent scope are queued up to be emitted (or the port's
  // source is queued) if they are not already pending.
  private Token CreatePorts(TokenEmissionState state, NodePos parentPos,
                            Node[] nodes, NodePos forPos) {
    Node parentNode = nodes[parentPos.NodeId];
    NodeTemplate parentTemplate = _nodeTemplates[parentPos.NodeId];
    if (!parentNode.IsConnected()) {
      throw new InvalidFormatException(new NodePos[] { parentPos },
                                       "parent-disconnected");
    }
    Token ret = null;
    // This handles enqueuing the parent node if necessary.
    Token source = state.GetOrCreateSource(parentNode.Source);
    foreach (int childId in parentTemplate.ChildIds) {
      NodePos portPos = new(parentPos.Block, childId);
      NodeTemplate child = _nodeTemplates[childId];
      if (child.Network == NetworkType.Placeholder) {
        continue;
      }
      Token port = ((IAcceptPort)source)
                       .AddPort(state, portPos, child.Name, child.IsSource);

      // Possibly enqueue the port to be emitted by its source.
      if (forPos != portPos) {
        AddPendingNodeSource(state, portPos, nodes);
      } else {
        ret = port;
      }

      if (forPos != parentPos) {
        // AddRef the port for the parent node.
        if (port.PendingRef == 0 || !state.PreparedContains(portPos, port)) {
          state.AddPrepared(portPos, port, parentPos);
        } else {
          port.AddRef(state, parentPos);
        }
      }
      // AddRef the port again for each child node (including the port itself).
      foreach (int childId2 in parentTemplate.ChildIds) {
        NodeTemplate child2 = _nodeTemplates[childId2];
        if (child2.Network == NetworkType.Placeholder) {
          continue;
        }
        NodePos childPos2 = new(parentPos.Block, childId2);
        if (port.PendingRef == 0 || !state.PreparedContains(portPos, port)) {
          state.AddPrepared(portPos, port, childPos2);
        } else {
          port.AddRef(state, childPos2);
        }
      }
    }
    return ret;
  }

  public override Token Emit(TokenEmissionState state, NodePos pos,
                             Node[] nodes, string inventoryTerm) {
    NodeTemplate nodeTemplate = _nodeTemplates[pos.NodeId];
    if (nodeTemplate.ParentId != -1 &&
        nodeTemplate.Network != NetworkType.Placeholder) {
      // This node is a port
      if (!state.Prepared.TryGetValue(pos, out Token port)) {
        NodePos parentPos = new(pos.Block, nodeTemplate.ParentId);
        port = CreatePorts(state, parentPos, nodes, pos);
      }
      if (nodes[pos.NodeId].IsConnected()) {
        NodePos portSourcePos = nodes[pos.NodeId].Source;
        Token source = state.Prepared[portSourcePos];
        if (!nodeTemplate.IsSource) {
          source.AddSink(state, port);
        }
        source.AddPendingChildren(state, nodeTemplate.Network,
                                  GetDownstream(pos));
        // If the port is its own source, then its reference will be released
        // below.
        if (source != port) {
          source.ReleaseRef(state, pos);
        }
      }
      port.ReleaseRef(state, pos);
      return port;
    }

    bool anyFound = false;
    foreach (int childId in nodeTemplate.ChildIds) {
      NodePos portPos = new NodePos(pos.Block, childId);
      NodeTemplate paired = _nodeTemplates[childId];
      if (state.Prepared.TryGetValue(portPos, out Token port)) {
        // The port was already emitted.
        port.ReleaseRef(state, pos);
        anyFound = true;
      } else {
        Debug.Assert(!anyFound);
        CreatePorts(state, pos, nodes, pos);
        break;
      }
    }
    {
      Token source = state.GetOrCreateSource(nodes[pos.NodeId].Source);
      source.AddPendingChildren(state, nodeTemplate.Network,
                                GetDownstream(pos));
      source.AddConnector(state, nodeTemplate.Network, pos);
    }
    return null;
  }
}
