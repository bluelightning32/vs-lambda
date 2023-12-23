using System;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

public struct Node {
  public NodePos Source;
  public Scope Scope = Scope.None;
  // The index of the edge in this node that points to the parent node. If the
  // network is fully up to date, then the parent edges point to the source
  // block.
  public Edge Parent;
  public int PropagationDistance = Int32.MaxValue;

  public Node() {}

  public bool IsConnected() {
    return Source.IsSet() && PropagationDistance != Int32.MaxValue;
  }

  public bool IsDisconnected() {
    return Source.IsSet() && PropagationDistance == Int32.MaxValue;
  }

  public bool IsEjected() { return !Source.IsSet(); }

  public bool IsSource() { return PropagationDistance == 0; }

  public TreeAttribute ToTreeAttributes() {
    TreeAttribute tree = new TreeAttribute();
    if (Source.IsSet()) {
      tree.SetBlockPos("SourceBlock", Source.Block);
      tree.SetInt("SourceNodeId", Source.NodeId);
      tree.SetInt("Scope", (int)Scope);
      tree.SetInt("Parent", (int)Parent);
      tree.SetInt("PropagationDistance", PropagationDistance);
    }
    return tree;
  }

  public bool FromTreeAttributes(TreeAttribute tree) {
    Source.Block = tree.GetBlockPos("SourceBlock", null);
    Source.NodeId = tree.GetAsInt("SourceNodeId", 0);
    Scope oldScope = Scope;
    Scope = (Scope)tree.GetAsInt("Scope", (int)Scope.None);
    Parent = (Edge)tree.GetAsInt("Parent", (int)Edge.Unknown);
    PropagationDistance = tree.GetAsInt("PropagationDistance", Int32.MaxValue);
    return oldScope != Scope;
  }

  public override readonly string ToString() {
    return $"source=&lt;{Source.Block?.ToString() ?? "null"}&gt;:{Source.NodeId}, parent={Parent}, dist={PropagationDistance}";
  }
}

[JsonObject(MemberSerialization.OptIn)]
public class NodeTemplate {
  public int Id = 0;
  public bool Source = false;
  [JsonProperty]
  public Scope SourceScope = Scope.Function;
  [JsonProperty]
  public Edge[] Edges = Array.Empty<Edge>();
  [JsonProperty]
  public string[] Textures = Array.Empty<string>();

  public NodeTemplate() {}

  public Scope GetScope(Node[] nodes) {
    if (Source) {
      return SourceScope;
    } else {
      return nodes[Id].Scope;
    }
  }

  public void OnPlaced(NetworkManager manager, BlockPos pos, bool scopeNetwork,
                       BlockNodeTemplate[] neighborTemplates,
                       Node[][] neighbors, ref Node node) {
    manager.Debug("Block placed on {0} source set: {1}", manager.Side,
                  node.Source.IsSet());
    foreach (Edge edge in Edges) {
      BlockFacing face = edge.GetFace();
      if (face == null) {
        // The source edge does not have a face.
        continue;
      }
      NodeTemplate neighborTemplate =
          neighborTemplates[face.Index]?.GetNodeTemplate(scopeNetwork,
                                                         edge.GetOpposite());
      if (neighborTemplate == null) {
        continue;
      }
      var neighborNode = neighbors[face.Index][neighborTemplate.Id];
      if (neighborNode.Source.IsSet()) {
        node.Source = neighborNode.Source;
        node.Scope = neighborNode.Scope;
        node.Parent = edge;
        node.PropagationDistance =
            neighborNode.PropagationDistance + manager.DefaultDistanceIncrement;
        NodePos nodePos = new NodePos(pos, Id);
        // manager.Accessor.SetNode(nodePos, in node);
        break;
      }
    }
  }

  public bool CanPlace(NetworkManager manager, BlockPos pos, bool scopeNetwork,
                       BlockNodeTemplate[] neighborTemplates,
                       Node[][] neighbors, ref string failureCode) {
    NodePos source = new NodePos();
    if (Source) {
      source.Block = pos;
      source.NodeId = Id;
    }
    foreach (Edge edge in Edges) {
      BlockFacing face = edge.GetFace();
      if (face == null) {
        // The source edge does not have a face.
        continue;
      }
      NodeTemplate neighborTemplate =
          neighborTemplates[face.Index]?.GetNodeTemplate(scopeNetwork,
                                                         edge.GetOpposite());
      if (neighborTemplate == null) {
        continue;
      }

      NodePos neighborSource =
          neighbors[face.Index][neighborTemplate.Id].Source;
      if (neighborSource.IsSet()) {
        if (source.IsSet()) {
          if (source != neighborSource) {
            failureCode = "conflictingsources";
            return false;
          }
        } else {
          source = neighborSource;
        }
      }
    }
    return true;
  }
}