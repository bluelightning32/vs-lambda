using System;
using System.Collections.Generic;
using System.Diagnostics;

using Newtonsoft.Json;

using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.Network;

public struct Node {
  public NodePos Source;
  public Scope Scope = Scope.None;
  // The index of the edge in this node that points to the parent node. If the
  // network is fully up to date, then the parent edges point to the source
  // block.
  //
  // This default initializes to Edge.Unknown.
  public Edge Parent;
  public static readonly int InfDistance = Int32.MaxValue;
  public int PropagationDistance = InfDistance;
  public bool HasInfDistance {
    get { return PropagationDistance == InfDistance; }
  }

  public Node() {}

  // Array allocation does not call the struct default constructor. See
  // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-10.0/parameterless-struct-constructors#array-allocation.
  //
  // So call this on all members of the array after allocating the array.
  public static void ArrayInitialize(Node[] nodes) {
    for (int i = 0; i < nodes.Length; ++i) {
      nodes[i].PropagationDistance = InfDistance;
    }
  }

  public bool IsConnected() { return Source.IsSet() && !HasInfDistance; }

  public bool IsDisconnected() { return Source.IsSet() && HasInfDistance; }

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
    PropagationDistance = tree.GetAsInt("PropagationDistance", InfDistance);
    return oldScope != Scope;
  }

  public override readonly string ToString() {
    return $"source=<{Source.Block?.ToString() ?? "null"}>:{Source.NodeId}, parent={Parent}, dist={PropagationDistance}";
  }

  public void Connect(Manager networkManager, Node node, Edge parentEdge) {
    Source = node.Source;
    Scope = node.Scope;
    Parent = parentEdge;
    Debug.Assert(!node.HasInfDistance);
    PropagationDistance =
        node.PropagationDistance + networkManager.DefaultDistanceIncrement;
  }

  public void SetDisconnected() {
    Parent = Edge.Unknown;
    PropagationDistance = InfDistance;
  }

  public void SetEjected() {
    SetDisconnected();
    Scope = Scope.None;
    Source.Block = null;
    Source.NodeId = 0;
  }
}

[JsonObject(MemberSerialization.OptIn)]
public class NodeTemplate {
  [JsonProperty]
  public string Name = "";
  [JsonProperty]
  public NetworkType Network = NetworkType.Scope;
  public int Id = 0;
  public bool Source = false;
  [JsonProperty]
  public Scope SourceScope = Scope.Function;
  [JsonProperty]
  public Edge[] Edges = Array.Empty<Edge>();
  [JsonProperty]
  public HashSet<string> Textures = new();
  [JsonProperty]
  public Dictionary<Scope, Dictionary<string, CompositeTexture>>
      ReplacementTextures = new();
  [JsonProperty]
  public string Parent;
  // Set to -1 if the node did not declare a parent. This is set by the
  // `BlockNodeTemplate` constructor.
  public int ParentId;
  // Ids of all nodes that declared this one as a parent. This is set by the
  // `BlockNodeTemplate` constructor.
  public int[] ChildIds = Array.Empty<int>();

  public NodeTemplate() {}

  public Scope GetScope(Node[] nodes) {
    if (Source) {
      return SourceScope;
    } else {
      return nodes[Id].Scope;
    }
  }

  public void OnPlaced(Manager manager, BlockPos pos,
                       BlockNodeTemplate[] neighborTemplates,
                       Node[][] neighbors, ref Node node) {
    Debug.Assert(Source || !node.Source.IsSet());
    bool anyInfDistNeighbors = false;
    foreach (Edge edge in Edges) {
      BlockFacing face = edge.GetFace();
      if (face == null) {
        // The source edge does not have a face.
        continue;
      }
      NodeTemplate neighborTemplate =
          neighborTemplates[face.Index]?.GetNodeTemplate(Network,
                                                         edge.GetOpposite());
      if (neighborTemplate == null) {
        continue;
      }
      var neighborNode = neighbors[face.Index][neighborTemplate.Id];
      if (neighborNode.Source.IsSet() && !node.Source.IsSet()) {
        node.Source = neighborNode.Source;
        node.Scope = neighborNode.Scope;
        node.Parent = edge;
        node.PropagationDistance =
            neighborNode.PropagationDistance + manager.DefaultDistanceIncrement;
      }
      anyInfDistNeighbors |= neighborNode.HasInfDistance;
    }
    if (!node.HasInfDistance && anyInfDistNeighbors) {
      manager.EnqueueNode(node, pos, Id);
    }
  }

  public void OnRemoved(NodeAccessor accessor, Manager manager, BlockPos pos,
                        BlockNodeTemplate[] neighborTemplates,
                        Node[][] neighbors, in Node node) {
    if (!node.Source.IsSet()) {
      return;
    }
    manager.RemoveNode(node, pos, Id);
    foreach (Edge edge in Edges) {
      BlockFacing face = edge.GetFace();
      if (face == null) {
        // The source edge does not have a face.
        continue;
      }
      NodeTemplate neighborTemplate =
          neighborTemplates[face.Index]?.GetNodeTemplate(Network,
                                                         edge.GetOpposite());
      if (neighborTemplate == null) {
        continue;
      }
      var neighborNode = neighbors[face.Index][neighborTemplate.Id];
      if (neighborNode.Source == node.Source) {
        if (neighborNode.Parent == edge.GetOpposite()) {
          BlockPos neighborBlock = pos.AddCopy(face);
          manager.EnqueueNode(neighborNode, neighborBlock, neighborTemplate.Id);
          manager.EnqueueEjection(neighborNode, neighborBlock,
                                  neighborTemplate.Id);
        } else if (neighborNode.HasInfDistance) {
          BlockPos neighborBlock = pos.AddCopy(face);
          manager.EnqueueEjection(neighborNode, neighborBlock,
                                  neighborTemplate.Id);
        }
      }
    }
  }

  public bool CanPlace(Manager manager, BlockPos pos,
                       BlockNodeTemplate[] neighborTemplates,
                       Node[][] neighbors, out string failureCode) {
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
          neighborTemplates[face.Index]?.GetNodeTemplate(Network,
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
    failureCode = string.Empty;
    return true;
  }

  public void Expand(NodeAccessor accessor, Manager networkManager,
                     BlockPos pos, Node node) {
    if (ShouldPropagateConnection(accessor, networkManager, pos, node)) {
      PropagateConnection(accessor, networkManager, pos, node);
    } else {
      node.SetDisconnected();
      accessor.SetNode(pos, Id, in node);
      PropagateDisconnection(accessor, networkManager, pos, node);
    }
  }

  // Returns true if this node should propagate its source to its neighbors.
  // This function is private, because its debug asserts can fail if it is
  // called on a node other than the one that's getting expanded.
  private bool ShouldPropagateConnection(NodeAccessor accessor,
                                         Manager networkManager, BlockPos pos,
                                         Node node) {
    if (Source) {
      return true;
    }
    Debug.Assert(node.PropagationDistance != 0);
    Debug.Assert(!node.HasInfDistance);
    if (node.HasInfDistance) {
      return false;
    }
    NodeTemplate parentTemplate =
        accessor.GetNode(pos.AddCopy(node.Parent.GetFace()), Network,
                         node.Parent.GetOpposite(), out Node parent);
    return node.Source == parent.Source &&
           networkManager.IsPropagationDistanceInRange(
               parent.PropagationDistance, node.PropagationDistance);
  }

  private void PropagateConnection(NodeAccessor accessor,
                                   Manager networkManager, BlockPos pos,
                                   Node node) {
    BlockPos neighborPos = new(pos.dimension);
    foreach (Edge edge in Edges) {
      if (edge == node.Parent) {
        // The parent edge was already checked by `ShouldPropagateConnection`.
        continue;
      }
      BlockFacing face = edge.GetFace();
      if (face == null) {
        // The source edge does not have a face.
        continue;
      }
      neighborPos.Set(pos);
      neighborPos.Offset(face);
      NodeTemplate neighborTemplate = accessor.GetNode(
          neighborPos, Network, edge.GetOpposite(), out Node neighbor);
      if (neighborTemplate == null) {
        continue;
      }

      bool neighModified = false;
      int neighborDistance = neighbor.PropagationDistance;
      if (neighbor.HasInfDistance) {
        neighbor.Connect(networkManager, node, edge.GetOpposite());
        neighModified = true;
      } else if (neighbor.Parent == edge.GetOpposite()) {
        Debug.Assert(neighbor.Source == node.Source);
        if (networkManager.IsPropagationDistanceTooLow(node.PropagationDistance,
                                                       neighborDistance)) {
          neighbor.PropagationDistance =
              node.PropagationDistance + networkManager.MinDistanceIncrement;
          neighModified = true;
        } else if (networkManager.IsPropagationDistanceTooHigh(
                       node.PropagationDistance, neighborDistance)) {
          neighbor.PropagationDistance =
              node.PropagationDistance + networkManager.MaxDistanceIncrement;
          neighModified = true;
        }
      }
      if (neighModified) {
        // Copy `neighborPos` so that it is immutable.
        BlockPos neighborPosCopy = neighborPos.Copy();
        accessor.SetNode(neighborPosCopy, neighborTemplate.Id, in neighbor);
        networkManager.RequeueNode(neighborDistance, neighbor, neighborPosCopy,
                                   neighborTemplate.Id);
      }
    }
  }

  private void PropagateDisconnection(NodeAccessor accessor,
                                      Manager networkManager, BlockPos pos,
                                      Node node) {
    BlockPos neighborPos = new(pos.dimension);
    foreach (Edge edge in Edges) {
      BlockFacing face = edge.GetFace();
      // The face should not be null, because sources are never disconnected.
      Debug.Assert(face != null);
      neighborPos.Set(pos);
      neighborPos.Offset(face);
      NodeTemplate neighborTemplate = accessor.GetNode(
          neighborPos, Network, edge.GetOpposite(), out Node neighbor);
      if (neighborTemplate == null) {
        continue;
      }

      // Enqueue children, because they may need to be disconnected. Enqueue
      // connected neighbors because they may reconnect this node.
      if (neighbor.Source == node.Source && !neighbor.HasInfDistance) {
        BlockPos neighborPosCopy = neighborPos.Copy();
        networkManager.EnqueueNode(neighbor, neighborPosCopy,
                                   neighborTemplate.Id);
      }
    }
  }

  public void EjectIfDisconnected(NodeAccessor accessor, Manager networkManager,
                                  NodePos source, BlockPos pos, Node node) {
    if (node.Source != source || !node.HasInfDistance) {
      // Do not eject this node if it is no longer disconnected, or if it now
      // belongs to a different source.
      return;
    }
    node.SetEjected();
    accessor.SetNode(pos, Id, in node);

    BlockPos neighborPos = new(pos.dimension);
    foreach (Edge edge in Edges) {
      BlockFacing face = edge.GetFace();
      if (face == null) {
        // The source edge does not have a face.
        continue;
      }
      neighborPos.Set(pos);
      neighborPos.Offset(face);
      NodeTemplate neighborTemplate = accessor.GetNode(
          neighborPos, Network, edge.GetOpposite(), out Node neighbor);
      if (neighborTemplate == null) {
        continue;
      }

      if (neighbor.Source == source && neighbor.HasInfDistance) {
        networkManager.EnqueueEjection(neighbor, neighborPos.Copy(),
                                       neighborTemplate.Id);
      }
    }
  }
}
